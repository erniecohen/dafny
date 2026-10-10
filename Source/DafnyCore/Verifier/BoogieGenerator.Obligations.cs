using System;
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
    IReadOnlyList<SplitExprInfo> Pieces, Bpl.Expr Summary, bool SplitHappened) {
    internal bool PurePreparation { get; init; }
  }

  // Structural tests observe only this check's freshly generated preparation.
  // The observer is absent during ordinary verification and changes no commands.
  internal record DeclaredPreparationSnapshot(Expression Source,
    IReadOnlyList<object> Commands, ISet<string> ArgumentTemporaries,
    Bpl.AssumeCmd LeadingSupport, bool LeadingSupportAfterPreparation, Bpl.Expr Heap);

  private PropositionLowering LowerProposition(BodyTranslationContext context, Expression condition,
    ExpressionTranslator etran, bool applyInduction = true, int heightLimit = int.MaxValue,
    ObligationPreparation preparation = ObligationPreparation.DeclaredContract, Bpl.Expr guard = null,
    bool universalClosure = false, ExpressionTranslator preparedTranslator = null) {
    var explicitAssertion = preparation == ObligationPreparation.CheckedExpression;
    var savedStatement = stmtContext;
    var savedAdjustment = adjustFuelForExists;
    // Lower the actual implicit check with the existing assertion policy at
    // this program point. Explicit assertions retain their original translator,
    // statement context, fuel choices and splitting decisions.
    var checking = preparedTranslator ?? (explicitAssertion ? etran : etran.CloneForObligation());
    try {
      if (!explicitAssertion && preparedTranslator == null) {
        stmtContext = StmtType.ASSERT;
        adjustFuelForExists = true;
      }
      var pieces = new List<SplitExprInfo>();
      var split = universalClosure
        ? TrSplitUniversalCheck(condition, pieces, checking)
        : TrSplitExpr(context, condition, pieces, true, heightLimit, applyInduction, checking);
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

  private PropositionLowering LowerDeclaredProposition(Expression condition,
    BoogieStmtListBuilder builder, Variables locals, ExpressionTranslator etran,
    Bpl.Expr preparationGuard = null, Bpl.AssumeCmd leadingSupport = null,
    ExpressionTranslator callerTerminationTranslator = null) {
    // Replay assertion-local setup using the contract's independently checked
    // non-read WF facts. Do not prove those facts twice or assume body reads
    // bounds: specification WF has a different reads policy. The same fresh
    // translator and consumed fuel state then lower the sole mandatory P check.
    // See docs/dev/obligation-certified-preparation.md for the licensing argument.
    var savedStatement = stmtContext;
    var savedAdjustment = adjustFuelForExists;
    var checking = etran.CloneForObligation();
    var callerChecking = (callerTerminationTranslator ?? builder.Context.CallerTerminationTranslator)?.CloneForObligation();
    try {
      stmtContext = StmtType.ASSERT;
      adjustFuelForExists = true;
      var argumentTemporaries = new HashSet<string>();
      var preparation = new BoogieStmtListBuilder(this, options,
        builder.Context with { AssertMode = AssertMode.Assume, CallerTerminationTranslator = callerChecking });
      BplIfIf(condition.Origin, preparationGuard != null, preparationGuard, preparation,
        guarded => TrStmt_CheckWellformed(condition, guarded, locals, checking, false,
          omitReadsAssertions: true, certifiedArgumentTemporaries: argumentTemporaries,
          callerTerminationTranslator: callerChecking));
      var normalized = CertifiedContractPreparation.Normalize(preparation.Commands, argumentTemporaries);
      // Match assertion-local support order only across certified pure setup.
      // Preserve the original placement if a write/havoc can change its truth,
      // or normalization rejected any part of the generated fragment.
      var supportAfterPreparation = leadingSupport != null &&
        !ReferenceEquals(normalized, preparation.Commands) &&
        CertifiedContractPreparation.CanMoveLeadingSupport(leadingSupport, normalized, argumentTemporaries);
      if (leadingSupport != null && !supportAfterPreparation) { builder.Add(leadingSupport); }
      builder.AppendAlreadyTranslated(preparation, normalized);
      if (supportAfterPreparation) { builder.Add(leadingSupport); }
      flags.ObligationPrepared?.Invoke(new DeclaredPreparationSnapshot(condition,
        normalized, argumentTemporaries, leadingSupport, supportAfterPreparation, checking.HeapExpr));
      return LowerProposition(builder.Context, condition, etran, preparedTranslator: checking) with {
        // Only the current proposition and its fresh preparation are inspected.
        // Statement expressions may reveal facts or change visibility; keep
        // their established outer effects in the original continuation.
        PurePreparation = !ReferenceEquals(normalized, preparation.Commands) &&
          !condition.DescendantsAndSelf.Any(expression => expression.Resolved is StmtExpr) &&
          CertifiedContractPreparation.CanScope(normalized, argumentTemporaries)
      };
    } finally {
      stmtContext = savedStatement;
      adjustFuelForExists = savedAdjustment;
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
    ProofObligationDescription description, BoogieStmtListBuilder builder, ExpressionTranslator etran,
    Func<Bpl.Expr, Bpl.Expr> close = null, bool forget = false, Bpl.QKeyValue attributes = null,
    bool universalClosure = false, ExpressionTranslator preparedTranslator = null) {
    close ??= expression => expression;
    // A guarded introduction does not grant its can-call premise unconditionally.
    var lowering = LowerProposition(builder.Context, condition, etran,
      preparation: ObligationPreparation.GuardedIntroduction, guard: guard, universalClosure: universalClosure,
      preparedTranslator: preparedTranslator);
    foreach (var piece in lowering.Pieces) {
      if (piece.IsChecked) {
        var token = ObligationOrigin(origin, piece.Tok);
        var check = close(BplImp(guard, piece.E));
        builder.Add(forget
          ? AssertAndForget(builder.Context, token, check, description, origin, attributes)
          : Assert(token, check, description, origin, builder.Context, attributes));
      }
    }
    if (lowering.SplitHappened && !forget) {
      // Ordinary split-assertion publication, after the sole checked pieces.
      var summary = TrAssumeCmd(origin, close(BplImp(guard, lowering.Summary)));
      proofDependencies?.AddProofDependencyId(summary, origin,
        new AssumptionDependency(false, "checked guarded obligation", condition));
      builder.Add(summary);
    }
  }

  internal void CheckOpaquePostcondition(IOrigin origin, Expression condition,
    ProofObligationDescription description, BoogieStmtListBuilder builder, ExpressionTranslator etran,
    Variables locals, Bpl.QKeyValue attributes) {
    if (options.Get(CommonOptionBag.ConsistentObligationChecks)) {
      // Use the immediate assertion's local WF path, before proving the block
      // clause. The outer contract-WF check occurs after block havoc and cannot
      // supply this body's local can-call/trigger preparation by itself.
      var savedStatement = stmtContext;
      var savedAdjustment = adjustFuelForExists;
      var checking = etran.CloneForObligation();
      try {
        stmtContext = StmtType.ASSERT;
        adjustFuelForExists = true;
        TrStmt_CheckWellformed(condition, builder, locals, checking, false);
        // Preparation can consume the one-shot existential adjustment. Keep
        // that state, as the immediate assertion does, for the sole P check.
        CheckPropositionUnderGuard(origin, condition, Bpl.Expr.True, description, builder, etran,
          attributes: attributes, preparedTranslator: checking);
      } finally {
        stmtContext = savedStatement;
        adjustFuelForExists = savedAdjustment;
      }
    } else {
      builder.Add(Assert(origin, etran.TrExpr(condition), description, builder.Context, attributes));
    }
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

  private void CheckMethodPostconditions(MethodOrConstructor method, IOrigin returnOrigin,
    BoogieStmtListBuilder builder, Variables locals, ExpressionTranslator etran) {
    CheckExitPostconditions(method.Ens, returnOrigin, builder, locals, etran, true);
  }

  private void CheckExitPostconditions(List<AttributedExpression> clauses, IOrigin returnOrigin,
    BoogieStmtListBuilder builder, Variables locals, ExpressionTranslator etran, bool reverifyInherited) {
    // Contract WF establishes the same declared permissions in clause order.
    // This is the sole implementation proof of each clause, at its actual exit
    // while body reveals are active. The procedure copy is nonchecking.
    if (assertionOnlyFilter != null) { return; }
    foreach (var ensures in ConjunctsOf(clauses)) {
      var inheritedClause = ensures.E.Origin.IsInherited(currentModule);
      // Local clauses receive the complete standard can-call support after WF
      // and before their sole actual check. Avoid constructing an extra copy
      // first: that also consumes assertion-local expression-construction state.
      // Preserve original leading support across inherited guards or statement
      // expressions, whose visibility effects are kept in the outer scope.
      var retainLeadingSupport = inheritedClause ||
        ensures.E.DescendantsAndSelf.Any(expression => expression.Resolved is StmtExpr);
      var leadingSupport = retainLeadingSupport
        ? TrAssumeCmd(ensures.E.Origin, etran.CanCallAssumption(ensures.E))
        : null;
      // An inherited clause must not introduce unguarded local WF assertions.
      // Locally declared clauses still receive their immediate assertion's WF,
      // even if splitting subsequently inlines an inherited callee expression.
      Bpl.Expr preparationGuard = inheritedClause
        ? reverifyInherited
          ? new Bpl.IdentifierExpr(returnOrigin, "$_reverifyPost", Bpl.Type.Bool)
          : Bpl.Expr.False
        : null;
      var lowering = LowerDeclaredProposition(ensures.E, builder, locals, etran, preparationGuard,
        leadingSupport: leadingSupport);
      var (error, success) = CustomErrorMessage(ensures.Attributes);
      var description = new EnsuresDescription(ensures.E, error, success);
      foreach (var piece in lowering.Pieces) {
        if (!piece.IsChecked) { continue; }
        var check = piece.E;
        if (piece.Tok.IsInherited(currentModule)) {
          if (!reverifyInherited) { continue; }
          check = BplImp(new Bpl.IdentifierExpr(returnOrigin, "$_reverifyPost", Bpl.Type.Bool), check);
        }
        // Retain ordinary checked-ensures publication and inherited guarding.
        // Force the actual contract check even in a source assume-mode region.
        builder.Add(Assert(new ForceCheckOrigin(ObligationOrigin(returnOrigin, piece.Tok)),
          check, description, builder.Context with { AssertMode = AssertMode.Check }));
      }
      var inherited = ensures.E.Origin.IsInherited(currentModule) ||
        lowering.Pieces.Any(piece => piece.IsChecked && piece.Tok.IsInherited(currentModule));
      Bpl.Expr guard = inherited && reverifyInherited
        ? new Bpl.IdentifierExpr(returnOrigin, "$_reverifyPost", Bpl.Type.Bool) : Bpl.Expr.True;
      if (lowering.SplitHappened && (reverifyInherited || !inherited)) {
        // A split check publishes its established source clause, as an assertion
        // does. An unsplit assertion already publishes its exact checked fact.
        var summary = TrAssumeCmd(returnOrigin, BplImp(guard, lowering.Summary));
        proofDependencies?.AddProofDependencyId(summary, returnOrigin,
          new AssumptionDependency(false, "checked method postcondition", ensures.E));
        builder.Add(summary);
      }
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
    // The existing allocation boxing bridge supplies the typed counterpart.
    // Check only the representation used by an explicit allocated assertion.
    return ExplicitAllocationPredicate(origin, value, type, etran.HeapExpr);
  }

  private Bpl.Expr ExplicitAllocationPredicate(IOrigin origin, Bpl.Expr value, Type type, Bpl.Expr heap) =>
    MkIsAllocBox(BoxIfNecessary(origin, value, type), type, heap);

  public partial class ExpressionTranslator {
    // Dev does not yet expose the supported line's old-heap rebasing helper.
    internal ExpressionTranslator WithOld(ExpressionTranslator old) {
      var clone = new ExpressionTranslator(this, HeapExpr);
      clone.oldEtran = old;
      return clone;
    }

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
