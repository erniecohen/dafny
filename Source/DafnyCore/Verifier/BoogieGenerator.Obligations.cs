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

  // Clone unresolved named identifiers, and rebind known formals and every
  // copied binder before traversing its body, attributes and trigger patterns.
  // Boogie's generic duplicator deliberately leaves identifier Decl references
  // alone, so cloning a binder without this map would change its binding.
  private sealed class MethodPostconditionDuplicator(Dictionary<Bpl.Variable, Bpl.Expr> replacements) : Bpl.Duplicator {
    public override Bpl.Expr VisitIdentifierExpr(Bpl.IdentifierExpr node) =>
      node.Decl != null && replacements.TryGetValue(node.Decl, out var replacement)
        ? replacement : base.VisitIdentifierExpr(node);

    private Bpl.Expr WithCopiedBindings(List<Bpl.Variable> variables,
      System.Func<List<Bpl.Variable>, Bpl.Expr> copyBody) {
      var copies = variables.Select(variable => {
        var copy = (Bpl.Variable)variable.Clone();
        copy.TypedIdent = (Bpl.TypedIdent)variable.TypedIdent.Clone();
        return copy;
      }).ToList();
      var saved = variables.Select(variable => replacements.GetValueOrDefault(variable)).ToList();
      foreach (var pair in variables.Zip(copies)) {
        replacements[pair.First] = new Bpl.IdentifierExpr(pair.Second.tok, pair.Second);
      }
      try {
        foreach (var pair in variables.Zip(copies)) {
          pair.Second.TypedIdent.Type = (Bpl.Type)Visit(pair.First.TypedIdent.Type);
          pair.Second.TypedIdent.WhereExpr = pair.First.TypedIdent.WhereExpr == null ? null : VisitExpr(pair.First.TypedIdent.WhereExpr);
          pair.Second.Attributes = pair.First.Attributes == null ? null : VisitQKeyValue(pair.First.Attributes);
        }
        return copyBody(copies);
      } finally {
        for (var i = 0; i < variables.Count; i++) {
          if (saved[i] == null) { replacements.Remove(variables[i]); }
          else { replacements[variables[i]] = saved[i]; }
        }
      }
    }

    public override Bpl.Expr VisitBinderExpr(Bpl.BinderExpr node) =>
      WithCopiedBindings(node.Dummies, copies => {
        var result = (Bpl.BinderExpr)node.Clone();
        result.Dummies = copies;
        result.Body = VisitExpr(node.Body);
        result.Attributes = node.Attributes == null ? null : VisitQKeyValue(node.Attributes);
        if (node is Bpl.QuantifierExpr quantifier) {
          ((Bpl.QuantifierExpr)result).Triggers = quantifier.Triggers == null ? null : VisitTrigger(quantifier.Triggers);
        }
        return result;
      });

    public override Bpl.QuantifierExpr VisitQuantifierExpr(Bpl.QuantifierExpr node) =>
      (Bpl.QuantifierExpr)VisitBinderExpr(node);

    public override Bpl.Expr VisitLetExpr(Bpl.LetExpr node) {
      // The right-hand sides are outside the let bindings' scope.
      var rhss = node.Rhss.Select(VisitExpr).ToList();
      return WithCopiedBindings(node.Dummies, copies => {
        var result = (Bpl.LetExpr)node.Clone();
        result.Dummies = copies;
        result.Rhss = rhss;
        result.Body = VisitExpr(node.Body);
        result.Attributes = node.Attributes == null ? null : VisitQKeyValue(node.Attributes);
        return result;
      });
    }
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
      }
      foreach (var original in clause.OriginalChecks) {
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
