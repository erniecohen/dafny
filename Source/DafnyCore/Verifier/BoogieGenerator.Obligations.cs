using System.Collections.Generic;
using System.Linq;
using DafnyCore.Verifier;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny;

public partial class BoogieGenerator {
  // This package constructs expressions only. Preparation permissions remain the
  // responsibility of the existing WF/declared-contract/guarded introduction path.
  internal enum ObligationPreparation { CheckedExpression, DeclaredContract, GuardedIntroduction }

  internal record PropositionInputs(BodyTranslationContext Body, ExpressionTranslator Translator,
    bool ApplyInduction, int InliningHeight, ObligationPreparation Preparation, Bpl.Expr Guard);

  internal record PropositionLowering(Expression Source, PropositionInputs Inputs,
    IReadOnlyList<SplitExprInfo> Pieces, Bpl.Expr Summary, bool SplitHappened);

  private PropositionLowering LowerProposition(BodyTranslationContext context, Expression condition,
    ExpressionTranslator etran, bool applyInduction = true, int heightLimit = int.MaxValue,
    ObligationPreparation preparation = ObligationPreparation.DeclaredContract, Bpl.Expr guard = null) {
    var explicitAssertion = preparation == ObligationPreparation.CheckedExpression;
    var savedStatement = stmtContext;
    var savedAdjustment = adjustFuelForExists;
    // Extra implicit checks start with the existing assertion policy. The
    // original checks and explicit assertions keep their original translator,
    // statement context, fuel choices and splitting decisions.
    var checking = explicitAssertion ? etran : etran.CloneForObligation();
    try {
      if (!explicitAssertion) {
        stmtContext = StmtType.ASSERT;
        adjustFuelForExists = true;
      }
      var pieces = new List<SplitExprInfo>();
      var split = TrSplitExpr(context, condition, pieces, true, heightLimit, applyInduction, checking);
      if (!split) {
        // TrAssertCondition has always translated the unsplit condition again.
        // Keep that exact formula, rather than replacing it by the splitter's
        // tentative result (which may have consumed a fuel adjustment).
        pieces.Clear();
        pieces.Add(ToSplitExprInfo(SplitExprInfo.K.Both, checking.TrExpr(condition)));
      }
      // Explicit assertions retain their existing statement publication path.
      // Construct a comparison summary there only for the structural observer.
      var summary = !explicitAssertion || flags.ObligationLowered != null
        ? AssertionSummary(condition, pieces, split, checking) : null;
      var lowering = new PropositionLowering(condition,
        new PropositionInputs(context, etran, applyInduction, heightLimit, preparation, guard), pieces, summary, split);
      flags.ObligationLowered?.Invoke(lowering);
      return lowering;
    } finally {
      if (!explicitAssertion) {
        stmtContext = savedStatement;
        adjustFuelForExists = savedAdjustment;
      }
    }
  }

  private Bpl.Expr AssertionSummary(Expression condition, IReadOnlyList<SplitExprInfo> pieces,
    bool split, ExpressionTranslator etran) {
    if (!split) { return pieces[0].E; }
    var savedStatement = stmtContext;
    var savedAdjustment = adjustFuelForExists;
    try {
      stmtContext = StmtType.ASSUME;
      adjustFuelForExists = true;
      return etran.CloneForObligation().TrExpr(condition);
    } finally {
      stmtContext = savedStatement;
      adjustFuelForExists = savedAdjustment;
    }
  }

  private void CheckPropositionUnderGuard(IOrigin origin, Expression condition, Bpl.Expr guard,
    ProofObligationDescription description, BoogieStmtListBuilder builder, ExpressionTranslator etran) {
    // A guarded introduction does not grant its can-call premise unconditionally.
    var lowering = LowerProposition(builder.Context, condition, etran,
      preparation: ObligationPreparation.GuardedIntroduction, guard: guard);
    foreach (var piece in lowering.Pieces) {
      if (piece.IsChecked) {
        builder.Add(AssertAndForget(builder.Context, ObligationOrigin(origin, piece.Tok), BplImp(guard, piece.E), description));
      }
    }
    var summary = TrAssumeCmd(origin, BplImp(guard, lowering.Summary));
    proofDependencies?.AddProofDependencyId(summary, origin,
      new AssumptionDependency(false, "checked guarded obligation", condition));
    builder.Add(summary);
  }

  private static IOrigin ObligationOrigin(IOrigin source, IOrigin piece) {
    // Synthesized built-in constraints can have NoToken origins. Related
    // locations must identify real source text; the obligation itself retains
    // the original boundary's location and message.
    static bool HasSourceLocations(IOrigin origin) {
      if (origin.Uri == null || origin.line <= 0) { return false; }
      return origin switch {
        NestedOrigin nested => HasSourceLocations(nested.Outer) && HasSourceLocations(nested.Inner),
        OriginWrapper wrapper => HasSourceLocations(wrapper.WrappedOrigin),
        _ => true
      };
    }
    return HasSourceLocations(piece) ? new NestedOrigin(source, piece) : source;
  }

  private record MethodPostconditionClause(AttributedExpression Source, IReadOnlyList<Bpl.Ensures> OriginalChecks);

  private readonly Dictionary<MethodOrConstructor, IReadOnlyList<MethodPostconditionClause>> methodPostconditionClauses = new();

  // Contract expressions are not resolved yet. Clone them without requiring a
  // declaration on every named identifier, rebinding only known formal objects.
  private sealed class MethodPostconditionDuplicator(Dictionary<Bpl.Variable, Bpl.Expr> formals) : Bpl.Duplicator {
    public override Bpl.Expr VisitIdentifierExpr(Bpl.IdentifierExpr node) =>
      node.Decl != null && formals.TryGetValue(node.Decl, out var replacement)
        ? replacement : base.VisitIdentifierExpr(node);
  }


  // Reuse only the same typed syntax at this scope and state. Bound names and
  // types, trigger lists and attributes must also match. No alpha-renaming,
  // fuel/boxing rewrite, let or unsupported expression can discharge a check.
  internal static bool SameContractCheck(Bpl.Expr left, Bpl.Expr right) =>
    SameContractCheck(left, right, new Dictionary<Bpl.Variable, Bpl.Variable>());

  private static bool SameContractCheck(Bpl.Expr left, Bpl.Expr right,
    Dictionary<Bpl.Variable, Bpl.Variable> bound) {
    if (left.GetType() != right.GetType() ||
        left is not (Bpl.IdentifierExpr or Bpl.LiteralExpr or Bpl.OldExpr or Bpl.NAryExpr or Bpl.QuantifierExpr)) {
      return false;
    }
    var leftType = left.Type ?? left.ShallowType;
    var rightType = right.Type ?? right.ShallowType;
    if (leftType == null || rightType == null || !leftType.Equals(rightType)) { return false; }
    bool Same(Bpl.Expr a, Bpl.Expr b) => SameContractCheck(a, b, bound);
    switch (left, right) {
      case (Bpl.IdentifierExpr a, Bpl.IdentifierExpr b):
        if (a.Name != b.Name) { return false; }
        return a.Decl == null && b.Decl == null ||
          a.Decl != null && b.Decl != null &&
          (bound.TryGetValue(a.Decl, out var paired) ? ReferenceEquals(paired, b.Decl) : ReferenceEquals(a.Decl, b.Decl));
      case (Bpl.LiteralExpr a, Bpl.LiteralExpr b):
        return a.ToString() == b.ToString();
      case (Bpl.OldExpr a, Bpl.OldExpr b):
        return Same(a.Expr, b.Expr);
      case (Bpl.NAryExpr a, Bpl.NAryExpr b):
        if (a.Fun.GetType() != b.Fun.GetType() || !ReferenceEquals(a.TypeParameters, b.TypeParameters)) { return false; }
        var sameFunction = (a.Fun, b.Fun) switch {
          (Bpl.FunctionCall f, Bpl.FunctionCall g) => f.FunctionName == g.FunctionName && ReferenceEquals(f.Func, g.Func),
          _ => a.Fun.Equals(b.Fun)
        };
        return sameFunction && a.Args.Count == b.Args.Count && a.Args.Zip(b.Args).All(pair => Same(pair.First, pair.Second));
      case (Bpl.QuantifierExpr a, Bpl.QuantifierExpr b):
        // Type parameters and where-clauses require additional binding rules.
        // Keep their original checks rather than approximating those rules.
        if (a.TypeParameters.Count != 0 || b.TypeParameters.Count != 0 || a.Dummies.Count != b.Dummies.Count) { return false; }
        var scoped = new Dictionary<Bpl.Variable, Bpl.Variable>(bound);
        foreach (var pair in a.Dummies.Zip(b.Dummies)) {
          if (pair.First.Name != pair.Second.Name ||
              !pair.First.TypedIdent.Type.Equals(pair.Second.TypedIdent.Type) ||
              pair.First.TypedIdent.WhereExpr != null || pair.Second.TypedIdent.WhereExpr != null ||
              !SameContractAttributes(pair.First.Attributes, pair.Second.Attributes, scoped)) { return false; }
          scoped.Add(pair.First, pair.Second);
        }
        if (!SameContractAttributes(a.Attributes, b.Attributes, scoped)) { return false; }
        var first = a.Triggers;
        var second = b.Triggers;
        while (first != null && second != null) {
          if (first.Pos != second.Pos || first.Tr.Count != second.Tr.Count ||
              !first.Tr.Zip(second.Tr).All(pair => SameContractCheck(pair.First, pair.Second, scoped))) { return false; }
          first = first.Next;
          second = second.Next;
        }
        return first == null && second == null && SameContractCheck(a.Body, b.Body, scoped);
      default:
        return false;
    }
  }

  private static bool SameContractAttributes(Bpl.QKeyValue left, Bpl.QKeyValue right,
    Dictionary<Bpl.Variable, Bpl.Variable> bound) {
    while (left != null && right != null) {
      if (left.Key != right.Key || left.Params.Count != right.Params.Count) { return false; }
      foreach (var pair in left.Params.Zip(right.Params)) {
        if (pair.First is Bpl.Expr a && pair.Second is Bpl.Expr b) {
          if (!SameContractCheck(a, b, bound)) { return false; }
        } else if (pair.First is Bpl.Expr || pair.Second is Bpl.Expr || !Equals(pair.First, pair.Second)) {
          return false;
        }
      }
      left = left.Next;
      right = right.Next;
    }
    return left == null && right == null;
  }

  private Bpl.PredicateCmd AssertMethodContract(IOrigin origin, Bpl.Expr condition,
    ProofObligationDescription description, BodyTranslationContext context) =>
    Assert(new ForceCheckOrigin(origin), condition, description,
      context with { AssertMode = AssertMode.Check });

  private void CheckMethodPostconditions(MethodOrConstructor method, IOrigin returnOrigin,
    BoogieStmtListBuilder builder, ExpressionTranslator etran) {
    // The declared contract WF procedure establishes permissions in clause order.
    // Check both the assertion-style pieces and the exact original contract
    // formulas before publishing a summary. Rechecking the same contract after
    // that summary creates an unnecessary cut and a different solver context.
    if (assertionOnlyFilter != null) { return; }
    if (!methodPostconditionClauses.TryGetValue(method, out var clauses)) {
      throw new System.InvalidOperationException("Missing original method postcondition checks");
    }
    foreach (var clause in clauses) {
      var ensures = clause.Source;
      builder.Add(TrAssumeCmd(ensures.E.Origin, etran.CanCallAssumptionForVerification(ensures.E)));
      var lowering = LowerProposition(builder.Context, ensures.E, etran);
      var (error, success) = CustomErrorMessage(ensures.Attributes);
      var description = new EnsuresDescription(ensures.E, error, success);
      var checkedPieces = new List<Bpl.Expr>();
      foreach (var piece in lowering.Pieces) {
        if (!piece.IsChecked) { continue; }
        var check = piece.E;
        if (piece.Tok.IsInherited(currentModule)) {
          check = BplImp(new Bpl.IdentifierExpr(returnOrigin, "$_reverifyPost", Bpl.Type.Bool), check);
        }
        // Checked procedure ensures publish each proved piece to the continuation.
        // Keep that policy for their local copies; explicit split assertions retain
        // their separate check-and-forget policy.
        builder.Add(AssertMethodContract(ObligationOrigin(returnOrigin, piece.Tok), check, description, builder.Context));
        checkedPieces.Add(check);
      }
      foreach (var original in clause.OriginalChecks) {
        if (checkedPieces.Any(piece => SameContractCheck(piece, original.Condition))) { continue; }
        builder.Add(AssertMethodContract(ObligationOrigin(returnOrigin, ToDafnyToken(original.tok)), original.Condition,
          description, builder.Context));
      }
      Bpl.Expr guard = ensures.E.Origin.IsInherited(currentModule) ||
        lowering.Pieces.Any(piece => piece.IsChecked && piece.Tok.IsInherited(currentModule))
        ? new Bpl.IdentifierExpr(returnOrigin, "$_reverifyPost", Bpl.Type.Bool) : Bpl.Expr.True;
      var summary = TrAssumeCmd(returnOrigin, BplImp(guard, lowering.Summary));
      proofDependencies?.AddProofDependencyId(summary, returnOrigin,
        new AssumptionDependency(false, "checked method postcondition", ensures.E));
      builder.Add(summary);
    }
  }

  private Bpl.Expr HigherOrderRequirement(IOrigin origin, int arity, IEnumerable<Bpl.Expr> arguments) =>
    FunctionCall(origin, Requires(arity), Bpl.Type.Bool, arguments.ToList());

  private Bpl.Expr AllocationObligation(IOrigin origin, Bpl.Expr value, Type type,
    ExpressionTranslator etran) {
    var legacy = GetWhereClause(origin, value, type, etran, ISALLOC, true);
    if (legacy == null || !options.Get(CommonOptionBag.ConsistentObligationChecks)) {
      return legacy;
    }
    return BplAnd(legacy, ExplicitAllocationPredicate(origin, value, type, etran.HeapExpr));
  }

  private Bpl.Expr ExplicitAllocationPredicate(IOrigin origin, Bpl.Expr value, Type type, Bpl.Expr heap) =>
    MkIsAllocBox(BoxIfNecessary(origin, value, type), type, heap);

  public partial class ExpressionTranslator {
    internal ExpressionTranslator CloneForObligation() =>
      CloneExpressionTranslator(this, BoogieGenerator, Predef, HeapExpr, This,
        applyLimited_CurrentFunction, layerInterCluster, layerIntraCluster,
        readsFrame, modifiesFrame, stripLits);

    internal ExpressionTranslator WithVerificationOldHeap(Bpl.Expr oldHeap) {
      var clone = CloneForObligation();
      var old = new ExpressionTranslator(BoogieGenerator, Predef, oldHeap, This,
        applyLimited_CurrentFunction, layerInterCluster, layerIntraCluster, scope,
        readsFrame, modifiesFrame, stripLits);
      old.oldEtran = old;
      clone.oldEtran = old;
      return clone;
    }
  }
}
