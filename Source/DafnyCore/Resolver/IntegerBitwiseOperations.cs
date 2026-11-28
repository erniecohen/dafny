// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

using System.Linq;
using System.Numerics;

namespace Microsoft.Dafny;

/// <summary>
/// Elaboration for bounded integer operations and source members. The ordinary call,
/// assertion and conversion machinery owns all permission, count and membership
/// obligations. No range theorem or defining equality is installed here.
/// </summary>
internal sealed class IntegerBitwiseOperations(ModuleResolver resolver) : ASTVisitor<IASTVisitorContext> {
  public override IASTVisitorContext GetContext(IASTVisitorContext context, bool inFunctionPostcondition) => context;

  // Used provisionally during inference. Validation below runs on resolved bounds,
  // independently of backend native-type selection, before verification or compilation.
  internal static int? DeclaredWidth(TopLevelDecl declaration) {
    if (declaration is not NewtypeDecl newtype || !newtype.IsRevealedInScope(Type.GetScope())) {
      return null;
    }
    var attributes = Attributes.FindAllExpressions(newtype.Attributes, "bitWidth");
    var args = attributes is { Count: 1 } ? attributes[0] : null;
    return args is { Count: 1 } && args[0] is LiteralExpr { Value: BigInteger n } && n > 0 && n <= int.MaxValue
      ? (int)n : null;
  }

  internal static int? DeclaredWidth(Type type) =>
    type.NormalizeExpand() is UserDefinedType { ResolvedClass: NewtypeDecl declaration } ? DeclaredWidth(declaration) : null;

  internal static bool Enabled(ModuleResolver resolver, IOrigin origin) =>
    resolver.Options.Get(CommonOptionBag.IntegerBitwise) && !(resolver.ProgramResolver.Program.Compilation.IntegerBitwiseDisabledSources?.Contains(origin.Uri) ?? false);

  protected override void VisitOneDeclaration(TopLevelDecl declaration) {
    if (declaration is NewtypeDecl newtype && newtype.IsRevealedInScope(Type.GetScope()) && Enabled(resolver, newtype.Origin) &&
        Attributes.Contains(newtype.Attributes, "bitWidth")) {
      Validate(newtype);
    }
    base.VisitOneDeclaration(declaration);
  }

  internal static bool IsSourceMember(string name) => name is "RotateLeft" or "RotateRight" or "bit" or "PopCount";

  internal static Expression ElaborateMember(ModuleResolver resolver, ExprDotName syntax, Type receiverType,
    System.Collections.Generic.List<ActualBinding> bindings) {
    var origin = syntax.Origin;
    var declaration = (NewtypeDecl)((UserDefinedType)receiverType.NormalizeExpand()).ResolvedClass;
    var width = DeclaredWidth(declaration).Value;
    var bit = syntax.SuffixName == "bit";
    var population = syntax.SuffixName == "PopCount";
    var parameter = bit ? "i" : "k";
    if (!Enabled(resolver, declaration.Origin) || !Enabled(resolver, origin)) {
      resolver.Reporter.Error(MessageSource.Resolver, syntax,
        "integer bitwise members require --integer-bitwise in both the use and the width declaration");
    }
    if (bindings.Count != (population ? 0 : 1) || bindings.Any(binding => binding.IsGhost ||
        binding.FormalParameterName != null && binding.FormalParameterName.val != parameter) ||
        syntax.OptTypeArguments is { Count: > 0 }) {
      resolver.Reporter.Error(MessageSource.Resolver, syntax,
        $"integer bitwise member '{syntax.SuffixName}' expects {(population ? "no arguments" : $"one integer argument '{parameter}'")} and no type arguments");
      return Expression.CreateBoolLiteral(origin, false);
    }
    Expression Number(int value) => new LiteralExpr(origin, value);
    Expression Integer(Expression value) => new ConversionExpr(origin, value, Type.Int);
    var count = population ? null : bindings[0].Actual;
    var references = resolver.SystemModuleManager.SystemModule.TopLevelDecls.OfType<IntegerBitwiseReferences>().Single();
    var receiver = new StaticReceiverExpr(origin, new UserDefinedType(origin, references.Name, references, []), false);
    var name = population ? "popNat" : bit ? "shrNat" : syntax.SuffixName == "RotateLeft" ? "rolNat" : "rorNat";
    var arguments = new System.Collections.Generic.List<ActualBinding> {
      new(null, Number(width)), new(null, Integer(syntax.Lhs))
    };
    if (!population) { arguments.Add(new(null, Integer(count))); }
    var call = new ApplySuffix(origin, null,
      new ExprDotName(origin, receiver, new Name(origin, name), null),
      arguments, null);
    // Compilation consumes exactly the source operands. Verification sees the
    // fixed integer reference and an ordinary result conversion. Population counts
    // target nat independently of the complete receiver predicate.
    Expression result = new ConversionExpr(origin, call, population ? resolver.SystemModuleManager.Nat() : bit ? Type.Int : receiverType) {
      IntegerBitwiseWidth = width
    };
    if (population) { return result; }
    if (bit) {
      result = new BinaryExpr(origin, BinaryExpr.Opcode.Eq,
        new BinaryExpr(origin, BinaryExpr.Opcode.Mod, result, Number(2)), Number(1));
    }
    var lower = new BinaryExpr(origin, BinaryExpr.Opcode.Le, Number(0), count);
    var upper = new BinaryExpr(origin, bit ? BinaryExpr.Opcode.Lt : BinaryExpr.Opcode.Le, count, Number(width));
    return new StmtExpr(origin, new AssertStmt(origin,
      new BinaryExpr(origin, BinaryExpr.Opcode.And, lower, upper), null, null) { IsGhost = true }, result);
  }

  private bool Validate(NewtypeDecl newtype) {
    var width = DeclaredWidth(newtype);
    if (width == null) {
      resolver.Reporter.Error(MessageSource.Resolver, newtype,
        ":bitWidth requires one positive integer literal no greater than 2147483647");
      return false;
    }
    if (newtype.Members.Any(member => IsSourceMember(member.Name)) ||
        resolver.GetClassMembers(newtype)?.Keys.Any(IsSourceMember) == true) {
      resolver.Reporter.Error(MessageSource.Resolver, newtype,
        ":bitWidth member names RotateLeft, RotateRight, bit and PopCount are reserved by --integer-bitwise");
      return false;
    }
    if (newtype.BaseType.NormalizeToAncestorType() is not IntType ||
        newtype.InvariantTypeDecl != null) {
      resolver.Reporter.Error(MessageSource.Resolver, newtype,
        ":bitWidth requires an integer newtype with no member invariants");
      return false;
    }
    BigInteger? lower = null;
    BigInteger? upper = null;
    var variable = newtype.Var ?? new BoundVar(newtype.Origin, "value", newtype.BaseType);
    var constraint = newtype.Constraint ?? Expression.CreateBoolLiteral(newtype.Origin, true);
    foreach (var bound in ModuleResolver.DiscoverAllBounds_SingleVar(variable, constraint, out _)) {
      void Accumulate(BigInteger? lo, BigInteger? hi) {
        if (lo != null && (lower == null || lower < lo)) { lower = lo; }
        if (hi != null && (upper == null || hi < upper)) { upper = hi; }
      }
      if (bound is IntBoundedPool range) {
        Accumulate(range.LowerBound == null ? null : ConstantFolder.TryFoldInteger(range.LowerBound),
          range.UpperBound == null ? null : ConstantFolder.TryFoldInteger(range.UpperBound));
      } else if (bound is ExactBoundedPool exact && ConstantFolder.TryFoldInteger(exact.E) is { } value) {
        Accumulate(value, value + 1);
      }
    }
    // Compare bit lengths, avoiding allocation of 2^N for arbitrarily large N.
    if (lower == null || lower < 0 || upper == null || upper < 0 ||
        upper > 0 && (upper.Value - 1).GetBitLength() > width.Value) {
      resolver.Reporter.Error(MessageSource.Resolver, newtype,
        ":bitWidth requires recognizable bounds establishing 0 <= value < 2^width");
      return false;
    }
    return true;
  }

  protected override void PostVisitOneExpression(Expression expression, IASTVisitorContext context) {
    if (expression is not IntegerBitwiseSyntaxExpr syntax) { return; }
    var source = syntax.Syntax;
    var left = source is BinaryExpr binary ? binary.E0 : ((UnaryOpExpr)source).E;
    var width = DeclaredWidth(left.Type);
    if (width == null) { return; } // Preserve native BV and Boolean operations.
    var newtype = (NewtypeDecl)((UserDefinedType)left.Type.NormalizeExpand()).ResolvedClass;
    if (!Enabled(resolver, syntax.Origin) || !Enabled(resolver, newtype.Origin) || !Validate(newtype)) {
      resolver.Reporter.Error(MessageSource.Resolver, syntax,
        "integer bitwise operators require --integer-bitwise in both the use and the width declaration");
      return;
    }
    var isShift = source is BinaryExpr { Op: BinaryExpr.Opcode.LeftShift or BinaryExpr.Opcode.RightShift };
    if (!syntax.Type.Equals(left.Type, true) || source is BinaryExpr b && !isShift && !b.E1.Type.Equals(left.Type, true)) {
      resolver.Reporter.Error(MessageSource.Resolver, syntax,
        "integer bitwise operands and result must have the same newtype; use explicit conversions");
      return;
    }
    var name = source is UnaryOpExpr ? "notNat" : ((BinaryExpr)source).Op switch {
      BinaryExpr.Opcode.BitwiseAnd => "andNat",
      BinaryExpr.Opcode.BitwiseOr => "orNat",
      BinaryExpr.Opcode.BitwiseXor => "xorNat",
      BinaryExpr.Opcode.LeftShift => "shlNat",
      BinaryExpr.Opcode.RightShift => "shrNat",
      _ => throw new System.InvalidOperationException("unexpected integer bitwise syntax")
    };
    var builtins = resolver.SystemModuleManager;
    var references = builtins.SystemModule.TopLevelDecls.OfType<IntegerBitwiseReferences>().Single();
    var receiverType = new UserDefinedType(syntax.Origin, references.Name, references, []);
    Expression Integer(Expression operand) => new ConversionExpr(operand.Origin, operand, Type.Int) { Type = Type.Int };
    var arguments = new System.Collections.Generic.List<Expression> {
      Expression.CreateIntLiteral(syntax.Origin, width.Value), Integer(left)
    };
    if (source is BinaryExpr operation) { arguments.Add(Integer(operation.E1)); }
    var call = Expression.CreateResolvedCall(syntax.Origin,
      new StaticReceiverExpr(syntax.Origin, receiverType, false) { Type = receiverType },
      references.References[name], arguments, [], builtins);
    Expression result = new ConversionExpr(syntax.Origin, call, syntax.Type) {
      Type = syntax.Type, IntegerBitwiseWidth = width.Value
    };
    if (isShift) {
      var count = arguments[2];
      var lower = new BinaryExpr(syntax.Origin, BinaryExpr.Opcode.Le,
        Expression.CreateIntLiteral(syntax.Origin, 0), count) { ResolvedOp = BinaryExpr.ResolvedOpcode.Le, Type = Type.Bool };
      var upper = new BinaryExpr(syntax.Origin, BinaryExpr.Opcode.Le,
        count, arguments[0]) { ResolvedOp = BinaryExpr.ResolvedOpcode.Le, Type = Type.Bool };
      var check = new AssertStmt(syntax.Origin, Expression.CreateAnd(lower, upper), null, null) { IsGhost = true };
      result = new StmtExpr(syntax.Origin, check, result) { Type = syntax.Type };
    }
    syntax.ResolvedExpression = result;
  }
}
