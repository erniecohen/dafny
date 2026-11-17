using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
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
    if (baseType.IsNumericBased() || baseType.IsBitVectorType || baseType.IsBoolType ||
        (baseType.IsCharType && options.Get(CommonOptionBag.LegacyUnguardedChar))) {
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

  /// <summary>
  /// Issue 105: the conversion of a value to a type D with member invariants (D &lt;- raw D). The value must be in
  /// raw D, and each invariant of D must hold of it. Each invariant is a separate obligation, "invariant might not
  /// hold", whose related location is that invariant. Returns false, and checks nothing, unless the target is such
  /// a type and its invariants are visible here; the caller then checks membership in the target as a whole.
  ///
  /// For a class C, the target is "C?" (C? &lt;- raw C?), whose invariants are checked of a value that is not null, or
  /// C (C &lt;- raw C?), for which the value must be in "raw C" before its invariants are checked.
  /// </summary>
  private bool CheckInvariantTypeMembership(IOrigin origin, Bpl.Expr value, Type sourceType, Type targetType,
    BoogieStmtListBuilder builder, ExpressionTranslator etran,
    Func<Bpl.Expr, Bpl.Expr> close, bool forget, bool universalClosure, Bpl.Expr valueCanCall) {
    var target = targetType.NormalizeExpandKeepConstraints();
    if (target is UserDefinedType { ResolvedClass: NonNullTypeDecl { Class.InvariantTypeDecl: { } classInvariants } and not RawNonNullTypeDecl } nonNull &&
        InvariantTypeDecl.IsRawReferenceType(sourceType) && RevealedInScope((RevealableTypeDecl)classInvariants)) {
      var rawNonNull = classInvariants.RawThisType(origin, nonNull.TypeArgs);
      CheckSubrange(origin, value, sourceType, rawNonNull, null, builder, etran: etran, close: close, forget: forget,
        universalClosure: universalClosure, valueCanCall: valueCanCall);
      target = classInvariants.FinishedType(origin, nonNull.TypeArgs);
    }
    if (target is not UserDefinedType { ResolvedClass: InvariantTypeDecl declaration } udt ||
        !RevealedInScope((RevealableTypeDecl)declaration)) {
      return false;
    }
    var typeMap = TypeParameter.SubstitutionMap(declaration.TypeArgs, udt.TypeArgs);
    var rawType = declaration.Var.Type.Subst(typeMap);
    CheckSubrange(origin, value, sourceType, rawType, null, builder, etran: etran, close: close, forget: forget,
      universalClosure: universalClosure, valueCanCall: valueCanCall);
    var wrapper = new BoogieWrapper(AdaptBoxing(origin, value, sourceType, rawType), rawType);
    var constraint = Substitute(declaration.Constraint, null,
      new Dictionary<IVariable, Expression> { { declaration.Var, wrapper } }, typeMap);
    Bpl.Expr notNull = null;
    if (InvariantTypeDecl.IsReferenceType(declaration.Owner)) {
      // The constraint of "C?" is "v != null ==> invariants"; check each invariant of a value that is not null
      var implication = (BinaryExpr)constraint.Resolved;
      Contract.Assert(implication.Op == BinaryExpr.Opcode.Imp);
      notNull = etran.TrExpr(implication.E0);
      constraint = implication.E1;
    }
    var description = new InvariantCheck(targetType.ToString());
    foreach (var conjunct in Expression.Conjuncts(constraint)) {
      var guard = etran.CanCallAssumption(conjunct);
      if (notNull != null) { guard = BplAnd(notNull, guard); }
      if (valueCanCall != null) { guard = BplAnd(valueCanCall, guard); }
      var check = close(BplImp(guard, etran.TrExpr(conjunct)));
      var token = conjunct.Resolved is FunctionCallExpr { Function: MemberInvariant invariant }
        ? new NestedOrigin(origin, invariant.Body.Origin, "this is the invariant that might not hold")
        : origin;
      builder.Add(forget
        ? AssertAndForget(builder.Context, token, check, description, origin, null)
        : Assert(token, check, description, origin, builder.Context));
    }
    return true;
  }

  /// <summary>
  /// Issue 105: an instance member is applied to a raw receiver, of type "raw D", only if it is a raw member of a type
  /// with member invariants, D or a trait that D extends; any other member, a member of a trait without invariants
  /// included, is applied only to a value of D. So if "receiver" is raw, check that it is one (D &lt;- raw D). A field
  /// whose value is stored needs no check: a raw value has it.
  /// </summary>
  private void CheckReceiverInvariants(IOrigin origin, MemberDecl member, Expression receiver,
    BoogieStmtListBuilder builder, ExpressionTranslator etran) {
    if (member is { IsStatic: true } or SpecialField || RawMemberInference.IsStoredField(member) ||
        (RawMemberInference.HasRawCounterpart(member) && RawMemberInference.IsRawMember(member)) ||
        !InvariantTypeDecl.IsRawType(receiver.Type)) {
      return;
    }
    var targetType = InvariantTypeDecl.FinishedTypeOf(receiver.Type);
    CheckSubrange(origin, etran.TrExpr(receiver), receiver.Type, targetType, receiver, builder, etran: etran);
  }

  private void CheckTypeMembership(IOrigin origin, Bpl.Expr membership, Bpl.Expr value,
    Type sourceType, Type targetType, ProofObligationDescription description,
    BoogieStmtListBuilder builder, ExpressionTranslator etran,
    Func<Bpl.Expr, Bpl.Expr> close = null, bool forget = false, bool universalClosure = false, Bpl.Expr valueCanCall = null) {
    close ??= expression => expression;
    if (etran != null && InvariantTypeDecl.FinishRawReferences(sourceType, targetType) is { } finished) {
      // Issue 105: a raw reference reaches a type that is not raw, such as "object", only if it satisfies the
      // invariants of its class. Check that first, and then the conversion of the finished value to the target.
      CheckSubrange(origin, value, sourceType, finished, null, builder, etran: etran, close: close, forget: forget,
        universalClosure: universalClosure, valueCanCall: valueCanCall);
      var rest = GetSubrangeCheck(origin, value, finished, targetType, null, null, out var restDescription);
      if (rest != null) {
        CheckTypeMembership(origin, rest, value, finished, targetType, restDescription, builder, etran, close, forget,
          universalClosure, valueCanCall);
      }
    } else if (etran != null && CheckInvariantTypeMembership(origin, value, sourceType, targetType, builder, etran, close, forget, universalClosure, valueCanCall)) {
      // Membership in the target follows from these checks under the target's introduction axiom,
      // "$Is(v, raw) && (CanCall ==> constraint) ==> $Is(v, target)", so nothing is assumed here.
    } else if (options.Get(CommonOptionBag.ConsistentObligationChecks) && etran != null &&
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
