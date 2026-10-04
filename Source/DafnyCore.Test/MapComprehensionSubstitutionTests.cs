using Microsoft.Dafny;
using Bpl = Microsoft.Boogie;
using DafnyType = Microsoft.Dafny.Type;

namespace DafnyCore.Test;

public class MapComprehensionSubstitutionTests {
  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void KeyOnlySubstitutionRebuildsMap(bool finite) {
    var original = CreateMap(finite, out var capturedKey);
    var replacement = Integer(7);

    var substituted = SubstituteKey(original, capturedKey, replacement);

    Assert.NotSame(original, substituted);
    Assert.Same(replacement, substituted.TermLeft);
    Assert.Same(original.Range, substituted.Range);
    Assert.Same(original.Term, substituted.Term);
    Assert.Same(original.BoundVars, substituted.BoundVars);
    Assert.Same(original.Type, substituted.Type);
    Assert.Equal(finite, substituted.Finite);
    Assert.Same(capturedKey, Assert.IsType<IdentifierExpr>(original.TermLeft).Var);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void SameBinderSubstitutionSharesDeclaredProjections(bool finite) {
    var original = CreateMap(finite, out var capturedKey);
    original.ProjectionFunctions = CreateProjections();

    var substituted = SubstituteKey(original, capturedKey, Integer(7));

    Assert.Same(original.BoundVars, substituted.BoundVars);
    Assert.Same(original, substituted.ProjectionFunctionsSource);
    Assert.Same(original.ProjectionFunctions, substituted.ProjectionFunctions);
    Assert.Same(original.ProjectionFunctions[0], substituted.ProjectionFunctions![0]);
    Assert.Same(original.ProjectionFunctions[1], substituted.ProjectionFunctions[1]);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void RepeatedSubstitutionSharesSourceBeforeProjectionsAreDeclared(bool finite) {
    var original = CreateMap(finite, out var capturedKey);
    var nextKey = new BoundVar(Token.NoToken, "nextKey", DafnyType.Int);
    var first = SubstituteKey(original, capturedKey, new IdentifierExpr(Token.NoToken, nextKey));
    var second = SubstituteKey(first, nextKey, Integer(7));

    Assert.NotSame(first, second);
    Assert.Same(original.BoundVars, second.BoundVars);
    Assert.Null(first.ProjectionFunctions);
    Assert.Null(second.ProjectionFunctions);
    Assert.Same(original, first.ProjectionFunctionsSource);
    Assert.Same(original, second.ProjectionFunctionsSource);

    // Declaration can happen after substitution; both copies still name its source.
    original.ProjectionFunctions = CreateProjections();
    Assert.Same(original.ProjectionFunctions, first.ProjectionFunctionsSource!.ProjectionFunctions);
    Assert.Same(original.ProjectionFunctions, second.ProjectionFunctionsSource!.ProjectionFunctions);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void OrdinaryCloneRebindsVariablesAndDropsProjectionMetadata(bool finite) {
    var original = CreateMap(finite, out var capturedKey);
    original.ProjectionFunctions = CreateProjections();
    var substituted = SubstituteKey(original, capturedKey, Integer(7));

    var clone = Assert.IsType<MapComprehension>(
      new Cloner(cloneResolvedFields: true).CloneExpr(substituted));

    Assert.Null(clone.ProjectionFunctions);
    Assert.Null(clone.ProjectionFunctionsSource);
    Assert.NotSame(substituted.BoundVars, clone.BoundVars);
    Assert.Equal(substituted.BoundVars.Count, clone.BoundVars.Count);
    for (var i = 0; i < clone.BoundVars.Count; i++) {
      Assert.NotSame(substituted.BoundVars[i], clone.BoundVars[i]);
    }
    Assert.Same(clone.BoundVars[0], Assert.IsType<IdentifierExpr>(clone.Term).Var);
  }

  private static MapComprehension CreateMap(bool finite, out BoundVar capturedKey) {
    var x = new BoundVar(Token.NoToken, "x", DafnyType.Int);
    var y = new BoundVar(Token.NoToken, "y", DafnyType.Bool);
    capturedKey = new BoundVar(Token.NoToken, "capturedKey", DafnyType.Int);
    return new MapComprehension(Token.NoToken, finite, [x, y],
      new LiteralExpr(Token.NoToken, true) { Type = DafnyType.Bool },
      new IdentifierExpr(Token.NoToken, capturedKey), new IdentifierExpr(Token.NoToken, x)) {
      Type = new MapType(finite, DafnyType.Int, DafnyType.Int)
    };
  }

  private static LiteralExpr Integer(int value) {
    return new LiteralExpr(Token.NoToken, value) { Type = DafnyType.Int };
  }

  private static MapComprehension SubstituteKey(MapComprehension map, BoundVar key, Expression replacement) {
    var substituter = new Substituter(null,
      new Dictionary<IVariable, Expression> { [key] = replacement },
      new Dictionary<TypeParameter, DafnyType>());
    return Assert.IsType<MapComprehension>(substituter.Substitute(map));
  }

  private static List<Bpl.Function> CreateProjections() {
    var predicateType = new Bpl.MapType(Token.NoToken, [], [Bpl.Type.Int, Bpl.Type.Bool], Bpl.Type.Bool);
    return [Projection("projectX", predicateType, Bpl.Type.Int),
      Projection("projectY", predicateType, Bpl.Type.Bool)];
  }

  private static Bpl.Function Projection(string name, Bpl.Type predicateType, Bpl.Type resultType) {
    var predicate = new Bpl.Formal(Token.NoToken,
      new Bpl.TypedIdent(Token.NoToken, "predicate", predicateType), true);
    var result = new Bpl.Formal(Token.NoToken,
      new Bpl.TypedIdent(Token.NoToken, "result", resultType), false);
    return new Bpl.Function(Token.NoToken, name, [predicate], result);
  }
}
