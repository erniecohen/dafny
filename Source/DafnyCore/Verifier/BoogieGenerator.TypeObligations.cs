using System;
using System.Collections.Generic;
using DafnyCore.Verifier;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny;

public partial class BoogieGenerator {
  private bool CheckVisibleTypeObligations(IOrigin origin, Bpl.Expr value, Type sourceType,
    Type targetType, ProofObligationDescription description, BoogieStmtListBuilder builder,
    ExpressionTranslator etran, Func<Bpl.Expr, Bpl.Expr> close = null, bool forget = false, bool universalClosure = false, Bpl.Expr valueCanCall = null) {
    if (targetType.NormalizeExpandKeepConstraints() is not UserDefinedType udt ||
        udt.ResolvedClass is not RedirectingTypeDecl declaration || declaration.Var == null ||
        declaration is NonNullTypeDecl || !RevealedInScope(udt.ResolvedClass) ||
        ArrowType.IsPartialArrowTypeName(declaration.Name) || ArrowType.IsTotalArrowTypeName(declaration.Name)) {
      return false;
    }

    var typeMap = TypeParameter.SubstitutionMap(declaration.TypeArgs, udt.TypeArgs);
    var baseType = declaration.Var.Type.Subst(typeMap);
    // The wrapper names the already translated value, never reevaluates the source
    // expression and retains its representation at this logical program state.
    var baseValue = AdaptBoxing(origin, value, sourceType, baseType);
    var wrapper = new BoogieWrapper(baseValue, baseType);
    Expression constraint;
    if (baseType.IsNumericBased() || baseType.IsBitVectorType || baseType.IsBoolType || baseType.IsCharType) {
      // This is the authoritative combined predicate used by the numeric $Is axiom.
      constraint = ModuleResolver.GetImpliedTypeConstraint(wrapper, udt);
    } else {
      CheckSubrange(origin, value, sourceType, baseType, null, builder, etran: etran, close: close, forget: forget, universalClosure: universalClosure, valueCanCall: valueCanCall);
      constraint = Substitute(declaration.Constraint, null,
        new Dictionary<IVariable, Expression> { { declaration.Var, wrapper } }, typeMap);
    }

    // Check the introduction axiom's CC => C without assuming CC. Base
    // membership is checked separately; symbolic target membership is then a
    // consequence of these checks under the existing introduction axiom.
    var guard = etran.CanCallAssumption(constraint);
    if (valueCanCall != null) { guard = BplAnd(valueCanCall, guard); }
    CheckPropositionUnderGuard(origin, constraint, guard, description, builder, etran, close, forget, universalClosure: universalClosure);
    return true;
  }

  private void CheckTypeMembership(IOrigin origin, Bpl.Expr membership, Bpl.Expr value,
    Type sourceType, Type targetType, ProofObligationDescription description,
    BoogieStmtListBuilder builder, ExpressionTranslator etran,
    Func<Bpl.Expr, Bpl.Expr> close = null, bool forget = false, bool universalClosure = false, Bpl.Expr valueCanCall = null) {
    close ??= expression => expression;
    if (options.Get(CommonOptionBag.ConsistentObligationChecks) && etran != null &&
        (CheckSequenceTypeMembership(origin, value, sourceType, targetType, description, builder, etran, close, forget, valueCanCall) ||
         CheckVisibleTypeObligations(origin, value, sourceType, targetType, description, builder, etran, close, forget, universalClosure, valueCanCall))) {
      // This is the existing introduction rule's derived representation of the
      // single constraint check, not an additional proof of the same constraint.
      if (!forget) {
        var guardedMembership = valueCanCall == null ? membership : BplImp(valueCanCall, membership);
        var derived = TrAssumeCmd(origin, close(guardedMembership));
        proofDependencies?.AddProofDependencyId(derived, origin,
          new AssumptionDependency(false, "derived checked type membership", new BoogieWrapper(close(guardedMembership), Type.Bool)));
        builder.Add(derived);
      }
    } else {
      var guardedMembership = valueCanCall == null ? membership : BplImp(valueCanCall, membership);
      builder.Add(forget
        ? AssertAndForget(builder.Context, origin, close(guardedMembership), description)
        : Assert(origin, close(guardedMembership), description, builder.Context));
    }
  }
  private bool CheckSequenceTypeMembership(IOrigin origin, Bpl.Expr value, Type sourceType,
    Type targetType, ProofObligationDescription description, BoogieStmtListBuilder builder,
    ExpressionTranslator etran, Func<Bpl.Expr, Bpl.Expr> close, bool forget, Bpl.Expr valueCanCall) {
    if (targetType.NormalizeExpandKeepConstraints() is not SeqType targetSequence ||
        sourceType.NormalizeToAncestorType() is not SeqType sourceSequence) {
      return false;
    }
    var sequence = AdaptBoxing(origin, value, sourceType, sourceSequence);
    var variable = new Bpl.BoundVariable(origin, new Bpl.TypedIdent(origin,
      CurrentIdGenerator.FreshId("$typeIndex#"), Bpl.Type.Int));
    var index = new Bpl.IdentifierExpr(origin, variable);
    var range = BplAnd(Bpl.Expr.Le(Bpl.Expr.Literal(0), index),
      Bpl.Expr.Lt(index, FunctionCall(origin, BuiltinFunction.SeqLength, null, sequence)));
    var boxed = FunctionCall(origin, BuiltinFunction.SeqIndex, Predef.BoxType, sequence, index);
    var element = UnboxUnlessInherentlyBoxed(boxed, sourceSequence.Arg);
    var membership = GetSubrangeCheck(origin, element, sourceSequence.Arg, targetSequence.Arg,
      null, null, out _);
    if (membership == null) { return false; }

    // The existing sequence membership axiom is exactly the range-bound
    // conjunction of element memberships. Close every actual element check
    // over that range; no index variable or local permission escapes it.
    CheckTypeMembership(origin, membership, element, sourceSequence.Arg, targetSequence.Arg,
      description, builder, etran,
      expression => close(new Bpl.ForallExpr(origin, [variable], BplImp(range, expression))), forget, universalClosure: true, valueCanCall: valueCanCall);
    return true;
  }

}
