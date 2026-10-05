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
    var checking = etran.WithVerificationUse(VerificationExpressionUse.Check);
    var pieces = new List<SplitExprInfo>();
    var split = TrSplitExpr(context, condition, pieces, true, heightLimit, applyInduction, checking);
    var summary = etran.WithVerificationUse(VerificationExpressionUse.Summary).TrExpr(condition);
    var lowering = new PropositionLowering(condition,
      new PropositionInputs(context, etran, applyInduction, heightLimit, preparation, guard), pieces, summary, split);
    flags.ObligationLowered?.Invoke(lowering);
    return lowering;
  }

  private void CheckPropositionUnderGuard(IOrigin origin, Expression condition, Bpl.Expr guard,
    ProofObligationDescription description, BoogieStmtListBuilder builder, ExpressionTranslator etran) {
    // A guarded introduction does not grant its can-call premise unconditionally.
    var lowering = LowerProposition(builder.Context, condition, etran,
      preparation: ObligationPreparation.GuardedIntroduction, guard: guard);
    foreach (var piece in lowering.Pieces) {
      if (piece.IsChecked) {
        builder.Add(AssertAndForget(builder.Context, new NestedOrigin(origin, piece.Tok), BplImp(guard, piece.E), description));
      }
    }
    var summary = TrAssumeCmd(origin, BplImp(guard, lowering.Summary));
    proofDependencies?.AddProofDependencyId(summary, origin,
      new AssumptionDependency(false, "checked guarded obligation", condition));
    builder.Add(summary);
  }

  private void CheckMethodPostconditions(MethodOrConstructor method, IOrigin returnOrigin,
    BoogieStmtListBuilder builder, ExpressionTranslator etran) {
    // The declared contract WF procedure establishes permissions in clause order.
    // Check each clause locally at this exit, then publish its guarded summary.
    // The procedure's checked ensures remain as the final semantic bridge.
    if (assertionOnlyFilter != null) { return; }
    foreach (var ensures in ConjunctsOf(method.Ens)) {
      builder.Add(TrAssumeCmd(ensures.E.Origin, etran.CanCallAssumption(ensures.E)));
      var lowering = LowerProposition(builder.Context, ensures.E, etran);
      var (error, success) = CustomErrorMessage(ensures.Attributes);
      var description = new EnsuresDescription(ensures.E, error, success);
      foreach (var piece in lowering.Pieces) {
        if (!piece.IsChecked) { continue; }
        var check = piece.E;
        if (piece.Tok.IsInherited(currentModule)) {
          check = BplImp(new Bpl.IdentifierExpr(returnOrigin, "$_reverifyPost", Bpl.Type.Bool), check);
        }
        builder.Add(AssertAndForget(builder.Context, new NestedOrigin(returnOrigin, piece.Tok), check, description));
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
    return ExplicitAllocationPredicate(origin, value, type, etran.HeapExpr);
  }

  private Bpl.Expr ExplicitAllocationPredicate(IOrigin origin, Bpl.Expr value, Type type, Bpl.Expr heap) =>
    MkIsAllocBox(BoxIfNecessary(origin, value, type), type, heap);

  public partial class ExpressionTranslator {
    internal ExpressionTranslator WithVerificationUse(VerificationExpressionUse use) =>
      WithVerificationContext(new VerificationExpressionContext(use)).StartFuelTracking();

    private ExpressionTranslator WithVerificationContext(VerificationExpressionContext context) =>
      CloneExpressionTranslator(this, BoogieGenerator, Predef, HeapExpr, This,
        applyLimited_CurrentFunction, layerInterCluster, layerIntraCluster, readsFrame,
        modifiesFrame, stripLits, context);

    internal ExpressionTranslator WithVerificationPolarity(bool positive) => verificationContext == null
      ? this : WithVerificationContext(verificationContext with { Positive = positive });

    internal ExpressionTranslator WithSelectedVerificationFuel() => verificationContext == null
      ? this : WithVerificationContext(verificationContext.FuelSelected());

    internal ExpressionTranslator AsVerificationValue() => verificationContext == null
      ? this : WithVerificationContext(verificationContext with { Use = VerificationExpressionUse.Value });

    private ExpressionTranslator NegateVerificationPolarity() => verificationContext == null
      ? this : WithVerificationContext(verificationContext.Negated());
  }
}
