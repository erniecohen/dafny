using System;
using System.Collections.Generic;
using DafnyCore.Verifier;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny;

public partial class BoogieGenerator {
  private bool CheckVisibleTypeObligations(IOrigin origin, Bpl.Expr value, Type sourceType,
    Type targetType, ProofObligationDescription description, BoogieStmtListBuilder builder,
    ExpressionTranslator etran, Func<Bpl.Expr, Bpl.Expr> close = null, bool forget = false) {
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
      CheckSubrange(origin, value, sourceType, baseType, null, builder, etran: etran, close: close, forget: forget);
      constraint = Substitute(declaration.Constraint, null,
        new Dictionary<IVariable, Expression> { { declaration.Var, wrapper } }, typeMap);
    }

    // Check the introduction axiom's CC => C without assuming CC. Base
    // membership is checked separately; symbolic target membership is then a
    // consequence of these checks under the existing introduction axiom.
    var guard = etran.CanCallAssumption(constraint);
    CheckPropositionUnderGuard(origin, constraint, guard, description, builder, etran, close, forget);
    return true;
  }

  private void CheckTypeMembership(IOrigin origin, Bpl.Expr membership, Bpl.Expr value,
    Type sourceType, Type targetType, ProofObligationDescription description,
    BoogieStmtListBuilder builder, ExpressionTranslator etran,
    Func<Bpl.Expr, Bpl.Expr> close = null, bool forget = false) {
    close ??= expression => expression;
    if (options.Get(CommonOptionBag.ConsistentObligationChecks) && etran != null &&
        CheckVisibleTypeObligations(origin, value, sourceType, targetType, description, builder, etran, close, forget)) {
      // This is the existing introduction rule's derived representation of the
      // single constraint check, not an additional proof of the same constraint.
      if (!forget) {
        var derived = TrAssumeCmd(origin, close(membership));
        proofDependencies?.AddProofDependencyId(derived, origin,
          new AssumptionDependency(false, "derived checked type membership", new BoogieWrapper(close(membership), Type.Bool)));
        builder.Add(derived);
      }
    } else {
      builder.Add(forget
        ? AssertAndForget(builder.Context, origin, close(membership), description)
        : Assert(origin, close(membership), description, builder.Context));
    }
  }
}
