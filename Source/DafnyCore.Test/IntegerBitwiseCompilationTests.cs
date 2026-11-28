using Microsoft.Dafny;
using DafnyType = Microsoft.Dafny.Type;

namespace DafnyCore.Test;

public class IntegerBitwiseCompilationTests {
  [Theory]
  [InlineData(12)]
  [InlineData(64)]
  public void ResolvedClonePreservesRuntimeSelection(int width) {
    var expression = Conversion(width, out _);
    var clone = Assert.IsType<ConversionExpr>(new Cloner(cloneResolvedFields: true).CloneExpr(expression));
    Assert.Equal(width, clone.IntegerBitwiseWidth);
    Assert.NotSame(expression.E, clone.E);
  }

  [Theory]
  [InlineData(12)]
  [InlineData(64)]
  public void SubstitutionPreservesRuntimeSelectionAndSemanticChild(int width) {
    var expression = Conversion(width, out var variable);
    var replacement = new LiteralExpr(Token.NoToken, 7) { Type = DafnyType.Int };
    var substituter = new Substituter(null, new Dictionary<IVariable, Expression> { [variable] = replacement },
      new Dictionary<TypeParameter, DafnyType>());
    var substituted = Assert.IsType<ConversionExpr>(substituter.Substitute(expression));
    Assert.Equal(width, substituted.IntegerBitwiseWidth);
    Assert.Same(replacement, substituted.E);
    Assert.Same(expression.Type, substituted.Type);
  }

  [Fact]
  public void OrdinaryConversionHasNoRuntimeSelection() {
    var expression = Conversion(null, out _);
    Assert.Null(Assert.IsType<ConversionExpr>(new Cloner(cloneResolvedFields: true).CloneExpr(expression)).IntegerBitwiseWidth);
  }

  [Theory]
  [InlineData(false, 0)]
  [InlineData(false, 1)]
  [InlineData(true, 0)]
  [InlineData(true, 1)]
  public void OnlySourceOperandsDetermineGhostness(bool ghost, int index) {
    var operands = new List<Expression> {
      new LiteralExpr(Token.NoToken, 8) { Type = DafnyType.Int }
    };
    for (var i = 0; i < 2; i++) {
      var variable = new BoundVar(Token.NoToken, "operand" + i, DafnyType.Int, ghost && i == index);
      operands.Add(new ConversionExpr(Token.NoToken, new IdentifierExpr(Token.NoToken, variable), DafnyType.Int) {
        Type = DafnyType.Int
      });
    }
    var call = new FunctionCallExpr(Token.NoToken, new Name(Token.NoToken, "andNat"),
      new ThisExpr(Token.NoToken) { Type = DafnyType.Int }, Token.NoToken, null, operands) { Type = DafnyType.Int };
    var expression = new ConversionExpr(Token.NoToken, call, DafnyType.Int) {
      Type = DafnyType.Int, IntegerBitwiseWidth = 8
    };
    Assert.Equal(ghost, ExpressionTester.UsesSpecFeatures(expression));
    Assert.Equal(!ghost, ExpressionTester.CheckIsCompilable(DafnyOptions.DefaultImmutableOptions, null, expression, null));
  }

  private static ConversionExpr Conversion(int? width, out BoundVar variable) {
    variable = new BoundVar(Token.NoToken, "operand", DafnyType.Int);
    return new ConversionExpr(Token.NoToken, new IdentifierExpr(Token.NoToken, variable), DafnyType.Int) {
      Type = DafnyType.Int, IntegerBitwiseWidth = width
    };
  }
}
