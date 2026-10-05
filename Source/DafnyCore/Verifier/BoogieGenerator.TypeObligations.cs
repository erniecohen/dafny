using System.Collections.Generic;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny;

public partial class BoogieGenerator {
  private void CheckVisibleTypeObligations(IOrigin origin, Bpl.Expr value, Type sourceType,
    Type targetType, ProofObligationDescription description, BoogieStmtListBuilder builder,
    ExpressionTranslator etran) {
    if (targetType.NormalizeExpandKeepConstraints() is not UserDefinedType udt ||
        udt.ResolvedClass is not RedirectingTypeDecl declaration || declaration.Var == null ||
        declaration is NonNullTypeDecl || !RevealedInScope(udt.ResolvedClass) ||
        ArrowType.IsPartialArrowTypeName(declaration.Name) || ArrowType.IsTotalArrowTypeName(declaration.Name)) {
      return;
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
      CheckSubrange(origin, value, sourceType, baseType, null, builder, etran: etran);
      constraint = Substitute(declaration.Constraint, null,
        new Dictionary<IVariable, Expression> { { declaration.Var, wrapper } }, typeMap);
    }

    // The introduction axiom's CC => C is checked without assuming CC. Base
    // membership and the original target $Is/$IsBox check remain as bridges.
    var guard = etran.CanCallAssumption(constraint);
    CheckPropositionUnderGuard(constraint, guard, description, builder, etran);
  }
}
