using System.Collections.Generic;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny;

public partial class BoogieGenerator {
  // This package constructs expressions only. Preparation permissions remain the
  // responsibility of the existing WF/declared-contract/guarded introduction path.
  private record PropositionLowering(Expression Source, IReadOnlyList<SplitExprInfo> Pieces,
    Bpl.Expr Summary, bool SplitHappened);

  private PropositionLowering LowerProposition(BodyTranslationContext context, Expression condition,
    ExpressionTranslator etran, bool applyInduction = true, int heightLimit = int.MaxValue) {
    var checking = etran.WithVerificationUse(VerificationExpressionUse.Check);
    var pieces = new List<SplitExprInfo>();
    var split = TrSplitExpr(context, condition, pieces, true, heightLimit, applyInduction, checking);
    var summary = etran.WithVerificationUse(VerificationExpressionUse.Summary).TrExpr(condition);
    return new PropositionLowering(condition, pieces, summary, split);
  }

  private void CheckPropositionUnderGuard(Expression condition, Bpl.Expr guard,
    ProofObligationDescription description, BoogieStmtListBuilder builder, ExpressionTranslator etran) {
    // A guarded introduction does not grant its can-call premise unconditionally.
    var lowering = LowerProposition(builder.Context, condition, etran);
    foreach (var piece in lowering.Pieces) {
      if (piece.IsChecked) {
        builder.Add(AssertAndForget(builder.Context, piece.Tok, BplImp(guard, piece.E), description));
      }
    }
    builder.Add(TrAssumeCmdWithDependenciesAndExtend(
      etran.WithVerificationUse(VerificationExpressionUse.Summary), condition.Origin, condition,
      _ => BplImp(guard, lowering.Summary), "checked guarded obligation"));
  }

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

    private ExpressionTranslator AsVerificationValue() => verificationContext == null
      ? this : WithVerificationContext(verificationContext with { Use = VerificationExpressionUse.Value });

    private ExpressionTranslator NegateVerificationPolarity() => verificationContext == null
      ? this : WithVerificationContext(verificationContext.Negated());
  }
}
