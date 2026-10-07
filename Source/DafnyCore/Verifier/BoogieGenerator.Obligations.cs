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

  private sealed class ContractFuelLayerDescription : ProofObligationDescription {
    public override string SuccessDescription => "existing fuel layers agree";
    public override string FailureDescription => "could not establish equivalence between existing fuel layers";
    public override string ShortDescription => "fuel layer agreement";
  }

  private void CheckContractFuelLayers(IOrigin origin, BoogieStmtListBuilder checking,
    List<(Bpl.Expr Condition, Expression Source)> facts, Expression source, IEnumerable<Bpl.Expr> expressions) {
    // Instantiate the existing unconditional layer-synonym law, then check the
    // instances before using them. No can-call permission or body visibility is
    // inferred, and no existing check or fuel argument is replaced.
    var fuelFunctions = declarationMapping.Where(pair => pair.Key is Function f && f.IsFuelAware())
      .ToDictionary(pair => pair.Value.Name, pair => (
        Position: Enumerable.Range(0, pair.Value.InParams.Count).FirstOrDefault(index =>
          pair.Value.InParams[index].TypedIdent.Name == "$ly" &&
          pair.Value.InParams[index].TypedIdent.Type == Predef.LayerType, -1), Arity: pair.Value.InParams.Count));
    var equalities = new List<Bpl.Expr>();
    var visited = new HashSet<Bpl.Expr>(ReferenceEqualityComparer.Instance);
    void Gather(Bpl.Expr expression) {
      // Do not extract applications from binders, lets or old-expression scopes.
      // Unsupported terms retain all existing checking and publication paths.
      if (expression is not Bpl.NAryExpr application || !visited.Add(expression)) { return; }
      if (application.Fun is Bpl.FunctionCall function &&
          fuelFunctions.TryGetValue(function.FunctionName, out var signature) &&
          signature.Position >= 0 && application.Args.Count == signature.Arity &&
          application.Args[signature.Position] is Bpl.NAryExpr successor && successor.Args.Count == 1 &&
          successor.Fun is Bpl.FunctionCall { FunctionName: "$LS" }) {
        var predecessor = (Bpl.NAryExpr)application.Clone();
        predecessor.Args = application.Args.ToList();
        predecessor.Args[signature.Position] = successor.Args[0];
        equalities.Add(Bpl.Expr.Eq(application, predecessor));
      }
      foreach (var argument in application.Args) { Gather(argument); }
    }
    foreach (var expression in expressions) { Gather(expression); }
    if (equalities.Count == 0) { return; }
    var agreement = equalities.Aggregate((left, right) => BplAnd(left, right));
    checking.Add(AssertMethodContract(origin, agreement, new ContractFuelLayerDescription(), checking.Context));
    facts.Add((agreement, source));
  }

  private sealed class ContractScopedFuelLayerDescription : ProofObligationDescription {
    public override string SuccessDescription => "existing fuel layers agree in their scope";
    public override string FailureDescription => "could not establish scoped equivalence between existing fuel layers";
    public override string ShortDescription => "scoped fuel layer agreement";
  }

  private void CheckContractQuantifiedFuelLayers(IOrigin origin, BoogieStmtListBuilder checking,
    List<(Bpl.Expr Condition, Expression Source)> facts, Expression source, IEnumerable<Bpl.Expr> expressions) {
    // These are checked instances of the same unconditional layer-synonym law
    // used above. Close over every referenced source dummy universally, for
    // either source quantifier polarity. Never export an existential witness.
    var fuelFunctions = declarationMapping.Where(pair => pair.Key is Function f && f.IsFuelAware())
      .ToDictionary(pair => pair.Value.Name, pair => (
        Position: Enumerable.Range(0, pair.Value.InParams.Count).FirstOrDefault(index =>
          pair.Value.InParams[index].TypedIdent.Name == "$ly" &&
          pair.Value.InParams[index].TypedIdent.Type == Predef.LayerType, -1), Arity: pair.Value.InParams.Count));
    var scopes = new List<List<Bpl.Variable>>();
    var equalities = new List<Bpl.Expr>();
    void Gather(Bpl.Expr expression, Dictionary<Bpl.Variable, Bpl.Expr> substitutions) {
      if (expression is Bpl.QuantifierExpr quantifier) {
        // Polymorphic Boogie binders need a separate type-binding copier.
        // Retain their original checks without extracting applications.
        if (quantifier.TypeParameters.Count != 0) { return; }
        var inner = new Dictionary<Bpl.Variable, Bpl.Expr>(substitutions);
        foreach (var dummy in quantifier.Dummies) { inner.Remove(dummy); }
        scopes.Add(quantifier.Dummies);
        Gather(quantifier.Body, inner);
        scopes.RemoveAt(scopes.Count - 1);
        return;
      }
      if (expression is Bpl.LetExpr let) {
        // Let right-hand sides are evaluated in the outer lexical scope.
        foreach (var rhs in let.Rhss) { Gather(rhs, substitutions); }
        var inner = new Dictionary<Bpl.Variable, Bpl.Expr>(substitutions);
        var copier = new MethodPostconditionDuplicator(substitutions);
        foreach (var pair in let.Dummies.Zip(let.Rhss)) {
          inner[pair.First] = copier.VisitExpr(pair.Second);
        }
        Gather(let.Body, inner);
        return;
      }
      // Do not extract from lambdas or move an application out of old(...).
      if (expression is not Bpl.NAryExpr original) { return; }
      if (scopes.Count != 0 && original.Fun is Bpl.FunctionCall function &&
          fuelFunctions.TryGetValue(function.FunctionName, out var signature) &&
          signature.Position >= 0 && original.Args.Count == signature.Arity) {
        var application = (Bpl.NAryExpr)new MethodPostconditionDuplicator(substitutions).VisitExpr(original);
        if (application.Args[signature.Position] is Bpl.NAryExpr successor && successor.Args.Count == 1 &&
            successor.Fun is Bpl.FunctionCall { FunctionName: "$LS" }) {
          var predecessor = (Bpl.NAryExpr)application.Clone();
          predecessor.Args = application.Args.ToList();
          predecessor.Args[signature.Position] = successor.Args[0];
          var equality = Bpl.Expr.Eq(application, predecessor);
          var free = new Bpl.GSet<object>();
          equality.ComputeFreeVariables(free);
          var needed = new List<Bpl.Variable>();
          var seen = new HashSet<Bpl.Variable>(ReferenceEqualityComparer.Instance);
          foreach (var variable in scopes.SelectMany(scope => scope)) {
            if (free.Contains(variable) && seen.Add(variable)) { needed.Add(variable); }
          }
          // Dependent or attributed dummies need a separate closure rule.
          // Skip only this additional instance; retain every original check.
          if (needed.Any(variable => variable.TypedIdent.WhereExpr != null || variable.Attributes != null)) { return; }
          Bpl.Expr agreement = equality;
          if (needed.Count != 0) {
            // Fresh names also keep distinct shadowed source declarations
            // distinct when their universal closure is flattened.
            var copies = needed.Select(variable => {
              var copy = (Bpl.Variable)variable.Clone();
              copy.TypedIdent = (Bpl.TypedIdent)variable.TypedIdent.Clone();
              copy.TypedIdent.Name = CurrentIdGenerator.FreshId("$contractFuel#");
              return copy;
            }).ToList();
            var replacements = needed.Zip(copies).ToDictionary(pair => pair.First,
              pair => (Bpl.Expr)new Bpl.IdentifierExpr(pair.Second.tok, pair.Second));
            var copier = new MethodPostconditionDuplicator(replacements);
            foreach (var pair in needed.Zip(copies)) {
              pair.Second.TypedIdent.Type = (Bpl.Type)copier.Visit(pair.First.TypedIdent.Type);
              pair.Second.TypedIdent.WhereExpr = pair.First.TypedIdent.WhereExpr == null
                ? null : copier.VisitExpr(pair.First.TypedIdent.WhereExpr);
              pair.Second.Attributes = pair.First.Attributes == null ? null : copier.VisitQKeyValue(pair.First.Attributes);
            }
            agreement = new Bpl.ForallExpr(origin, copies, BplTrigger(copier.VisitExpr(application)),
              copier.VisitExpr(equality));
          }
          equalities.Add(agreement);
        }
      }
      foreach (var argument in original.Args) { Gather(argument, substitutions); }
    }
    foreach (var expression in expressions) { Gather(expression, new Dictionary<Bpl.Variable, Bpl.Expr>()); }
    if (equalities.Count == 0) { return; }
    var conjunction = equalities.Aggregate((left, right) => BplAnd(left, right));
    checking.Add(AssertMethodContract(origin, conjunction, new ContractScopedFuelLayerDescription(), checking.Context));
    facts.Add((conjunction, source));
  }

  private sealed class ContractBodyEqualityDescription : ProofObligationDescription {
    public override string SuccessDescription => "available predicate definition agrees";
    public override string FailureDescription => "could not establish the available predicate definition";
    public override string ShortDescription => "predicate body agreement";
  }

  private void CheckContractBodyEqualities(IOrigin origin, BoogieStmtListBuilder checking,
    List<(Bpl.Expr Condition, Expression Source)> facts, PropositionLowering lowering) {
    // An inlined free piece already contains can-call && predicate && body.
    // Check the guarded folding equality before using it. The existing splitter
    // has enforced visibility, SCC height, no_inline and safe substitution.
    if (checking.Context.ContainsHide) { return; }
    var functions = declarationMapping.Where(pair => pair.Key is Function f && !f.IsFuelAware())
      .ToDictionary(pair => pair.Value.Name, pair => pair.Value);
    foreach (var piece in lowering.Pieces.Where(piece => piece.IsOnlyFree)) {
      if (piece.E is not Bpl.NAryExpr {
            Fun: Bpl.BinaryOperator { Op: Bpl.BinaryOperator.Opcode.And }, Args.Count: 2
          } preparation ||
          preparation.Args[0] is not Bpl.NAryExpr { Fun: Bpl.FunctionCall } permission ||
          preparation.Args[1] is not Bpl.NAryExpr {
            Fun: Bpl.BinaryOperator { Op: Bpl.BinaryOperator.Opcode.And }, Args.Count: 2
          } proposition ||
          proposition.Args[0] is not Bpl.NAryExpr { Fun: Bpl.FunctionCall } application ||
          !functions.TryGetValue(((Bpl.FunctionCall)application.Fun).FunctionName, out var function) ||
          ((Bpl.FunctionCall)permission.Fun).FunctionName != function.Name + "#canCall" ||
          application.Args.Count != function.InParams.Count) { continue; }
      var agreement = BplImp(permission, Bpl.Expr.Eq(application, proposition.Args[1]));
      // Keep the existing Boolean heap trigger as a guard and retain the
      // actual reveal flag. Neither guard is granted unconditionally.
      for (var i = function.InParams.Count - 1; i >= 0; i--) {
        if (function.InParams[i].TypedIdent.Type == Predef.HeapType) {
          agreement = BplImp(FunctionCall(origin, BuiltinFunction.IsGoodHeap, null, application.Args[i]), agreement);
        } else if (function.InParams[i].TypedIdent.Name == "$reveal" &&
                   function.InParams[i].TypedIdent.Type == Bpl.Type.Bool) {
          agreement = BplImp(application.Args[i], agreement);
        }
      }
      checking.Add(AssertMethodContract(origin, agreement, new ContractBodyEqualityDescription(), checking.Context));
      facts.Add((agreement, lowering.Source));
    }
  }

  private void PublishContractProofCut(IOrigin origin, BoogieStmtListBuilder continuation,
    BoogieStmtListBuilder checking, IReadOnlyList<(Bpl.Expr Condition, Expression Source)> facts) {
    // The verification arm checks every fact before becoming unreachable. The
    // continuation arm receives exactly those facts and the justified contract
    // permissions. Both arms stay in the existing VC; no batching marker or
    // visibility scope is introduced.
    if (facts.Count == 0) { return; }
    checking.Add(TrAssumeCmd(origin, Bpl.Expr.False));
    var publication = new BoogieStmtListBuilder(this, options, continuation.Context);
    foreach (var fact in facts) {
      var command = TrAssumeCmd(origin, fact.Condition);
      proofDependencies?.AddProofDependencyId(command, origin,
        new AssumptionDependency(false, "verified contract continuation", fact.Source));
      publication.Add(command);
    }
    continuation.Add(new Bpl.IfCmd(origin, null, checking.Collect(origin), null, publication.Collect(origin)));
  }

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
    var continuation = builder;
    builder = new BoogieStmtListBuilder(this, options, continuation.Context);
    var facts = new List<(Bpl.Expr Condition, Expression Source)>();
    foreach (var clause in clauses) {
      var ensures = clause.Source;
      var permission = etran.CanCallAssumptionForVerification(ensures.E);
      builder.Add(TrAssumeCmd(ensures.E.Origin, permission));
      facts.Add((permission, ensures.E));
      var lowering = LowerProposition(builder.Context, ensures.E, etran);
      CheckContractFuelLayers(ensures.E.Origin, builder, facts, ensures.E,
        new[] { lowering.Summary, permission }.Concat(lowering.Pieces.Select(piece => piece.E))
          .Concat(clause.OriginalChecks.Select(original => original.Condition)));
      CheckContractQuantifiedFuelLayers(ensures.E.Origin, builder, facts, ensures.E,
        new[] { lowering.Summary, permission }.Concat(lowering.Pieces.Select(piece => piece.E))
          .Concat(clause.OriginalChecks.Select(original => original.Condition)));
      CheckContractBodyEqualities(ensures.E.Origin, builder, facts, lowering);
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
        facts.Add((check, ensures.E));
      }
      foreach (var original in clause.OriginalChecks) {
        builder.Add(AssertMethodContract(ObligationOrigin(returnOrigin, ToDafnyToken(original.tok)), original.Condition,
          description, builder.Context));
        facts.Add((original.Condition, ensures.E));
      }
      Bpl.Expr guard = ensures.E.Origin.IsInherited(currentModule) ||
        lowering.Pieces.Any(piece => piece.IsChecked && piece.Tok.IsInherited(currentModule))
        ? new Bpl.IdentifierExpr(returnOrigin, "$_reverifyPost", Bpl.Type.Bool) : Bpl.Expr.True;
      // Check the exact publication form too. Ordinary assertion publication
      // retains its facts, triggers and fuel without a separate assumed cut.
      var summary = BplImp(guard, lowering.Summary);
      builder.Add(AssertMethodContract(ObligationOrigin(returnOrigin, ensures.E.Origin),
        summary, description, builder.Context));
      facts.Add((summary, ensures.E));
    }
    PublishContractProofCut(returnOrigin, continuation, builder, facts);
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
