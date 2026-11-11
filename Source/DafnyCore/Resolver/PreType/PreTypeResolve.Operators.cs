using System;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Dafny;

public partial class PreTypeResolver {
  private void ResolveOperator(OperatorSyntaxExpr op, ResolutionContext context) {
    OperatorSelection saved;
    try {
      saved = op.ReplaySelection();
    } catch (InvalidOperationException exception) {
      ReportError(op, exception.Message);
      op.PreType = CreatePreTypeProxy("invalid operator selection");
      return;
    }
    if (op.ResolutionStarted && saved == null) {
      ReportError(op, "resolved operator selection is missing; lookup cannot be restarted");
      op.PreType = CreatePreTypeProxy("missing operator selection");
      return;
    }
    foreach (var operand in op.Operands) {
      ResolveExpression(operand, context);
    }
    op.PreType = CreatePreTypeProxy("operator result");
    var operands = op.Operands.ToList();
    // Captured context and resolved operand objects survive lexical scope popping. Only
    // type constraints and already-selected call checks run here, never lexical lookup.
    Constraints.AddGuardedConstraint(() => {
      var left = operands[0].PreType.NormalizeWrtScope() as DPreType;
      if (left == null) {
        return false;
      }
      var right = operands.Count == 2 ? operands[1].PreType.NormalizeWrtScope() as DPreType : null;
      if (op.Symbol is "<" or ">" && right == null) {
        return false;
      }
      Function function = null;
      if (saved is MemberOperatorSelection member) {
        function = member.Member;
        if (member.Descriptor != new OperatorDescriptor(op.Symbol, operands.Count) ||
            !member.Receiver.Matches(left) || !resolver.VisibleInScope(function) ||
            !ReferenceEquals(function.EnclosingClass, left.Decl) || !OperatorResolution.SupportedOwner(left.Decl)) {
          ReportError(op, "operator selection cannot be transported into this owner/member view");
          return true;
        }
      } else if (saved is IntrinsicOperatorSelection || IntrinsicOperatorApplies(op, left, right)) {
        ResolveExpression(op.Syntax, context);
        op.ResolvedExpression = op.Syntax;
        AddSubtypeConstraint(op.PreType, op.Syntax.PreType, op.Origin, "operator result has type {1}, expected {0}");
        op.ResolutionStarted = true;
        return true;
      } else {
        if (!OperatorResolution.SupportedOwner(left.Decl)) {
          ReportError(op, "operator use requires an unsupported hidden, abstract, inherited or bounded owner view");
          return true;
        }
        ResolvePreTypeSignature(left.Decl);
        var owner = (TopLevelDeclWithMembers)left.Decl;
        foreach (var ownerMember in owner.Members) {
          ResolvePreTypeSignature(ownerMember);
        }
        OperatorResolution.Validate(new[] { owner }, resolver);
        var candidates = owner.Members.OfType<Function>().Where(f =>
          ReferenceEquals(f.EnclosingClass, owner) && resolver.VisibleInScope(f) &&
          f.Operator == new OperatorDescriptor(op.Symbol, operands.Count)).ToList();
        if (candidates.Count != 1) {
          ReportError(op, $"operator '{op.Symbol}' is not uniquely defined for left operand type '{left}'");
          return true;
        }
        function = candidates[0];
      }
      Expression receiver = operands[0];
      if (function.IsStatic) {
        // Type-only receiver: the left operand is already the first actual argument.
        receiver = new StaticReceiverExpr(op.Origin, new InferredTypeProxy(), true) {
          PreType = left.SansPrintablePreType()
        };
      }
      var selection = (MemberSelectExpr)ResolveExprDotCall(op.Origin, new Name(op.Origin, function.Name),
        receiver, left, function, null, null, context, false);
      var actuals = function.IsStatic ? operands : operands.Skip(1).ToList();
      var bindings = new ActualBindings(actuals.Select(a => new ActualBinding(null, a)).ToList());
      var call = ResolveSelectedFunctionCall(op.Origin, null, bindings, selection, context);
      call.IsOperatorCall = true;
      op.ResolvedExpression = call;
      AddSubtypeConstraint(op.PreType, call.PreType, op.Origin, "operator result has type {1}, expected {0}");
      op.ResolutionStarted = true;
      return true;
    });
  }

  private bool IntrinsicOperatorApplies(OperatorSyntaxExpr op, DPreType left, DPreType right) {
    if (op.Syntax is NegationExpression) {
      return Constraints.ConfirmConstraint(PreTypeConstraints.CommonConfirmationBag.NumericOrBitvector, left, null);
    }
    var opcode = ((BinaryExpr)op.Syntax).Op;
    if (opcode is BinaryExpr.Opcode.Lt or BinaryExpr.Opcode.Gt &&
        (left.Decl is IndDatatypeDecl && right?.Decl is IndDatatypeDecl or TypeParameter ||
         right?.Decl is IndDatatypeDecl && left.Decl is TypeParameter)) {
      return true;
    }
    var bag = opcode switch {
      BinaryExpr.Opcode.Add => PreTypeConstraints.CommonConfirmationBag.Plussable,
      BinaryExpr.Opcode.Sub => PreTypeConstraints.CommonConfirmationBag.Minusable,
      BinaryExpr.Opcode.Mul => PreTypeConstraints.CommonConfirmationBag.Mullable,
      BinaryExpr.Opcode.Div => PreTypeConstraints.CommonConfirmationBag.NumericOrBitvector,
      BinaryExpr.Opcode.Mod => PreTypeConstraints.CommonConfirmationBag.IntLikeOrBitvector,
      BinaryExpr.Opcode.Lt or BinaryExpr.Opcode.Le => PreTypeConstraints.CommonConfirmationBag.OrderableLess,
      _ => PreTypeConstraints.CommonConfirmationBag.OrderableGreater
    };
    return Constraints.ConfirmConstraint(bag, left, null);
  }
}
