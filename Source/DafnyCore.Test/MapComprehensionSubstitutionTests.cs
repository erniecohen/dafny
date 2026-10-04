using DafnyCore.Test.Resolver.Cardinality;
using Microsoft.Dafny;
using Bpl = Microsoft.Boogie;
using DafnyType = Microsoft.Dafny.Type;

namespace DafnyCore.Test;

[Collection("Cardinality resolution")]
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
  public void SameBinderSubstitutionSharesStableSourceIdentity(bool finite) {
    var original = CreateMap(finite, out var capturedKey);

    var substituted = SubstituteKey(original, capturedKey, Integer(7));

    Assert.Same(original.BoundVars, substituted.BoundVars);
    Assert.Same(original, substituted.ProjectionFunctionsSource);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void RepeatedSubstitutionSharesTheOriginalSource(bool finite) {
    var original = CreateMap(finite, out var capturedKey);
    var nextKey = new BoundVar(Token.NoToken, "nextKey", DafnyType.Int);
    var first = SubstituteKey(original, capturedKey, new IdentifierExpr(Token.NoToken, nextKey));
    var second = SubstituteKey(first, nextKey, Integer(7));

    Assert.NotSame(first, second);
    Assert.Same(original.BoundVars, second.BoundVars);
    Assert.Same(original, first.ProjectionFunctionsSource);
    Assert.Same(original, second.ProjectionFunctionsSource);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void OrdinaryCloneRebindsVariablesAndDropsProjectionMetadata(bool finite) {
    var original = CreateMap(finite, out var capturedKey);
    var substituted = SubstituteKey(original, capturedKey, Integer(7));

    var clone = Assert.IsType<MapComprehension>(
      new Cloner(cloneResolvedFields: true).CloneExpr(substituted));

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

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void TypeChangingSubstitutionCreatesANewProjectionSource(bool finite) {
    var parameter = new TypeParameter(Token.NoToken, new Name("T"), TPVarianceSyntax.NonVariant_Strict);
    var type = new UserDefinedType(parameter);
    var bound = new BoundVar(Token.NoToken, "x", type);
    var key = new BoundVar(Token.NoToken, "key", DafnyType.Int);
    var original = new MapComprehension(Token.NoToken, finite, [bound],
      new LiteralExpr(Token.NoToken, true) { Type = DafnyType.Bool },
      new IdentifierExpr(Token.NoToken, key), new IdentifierExpr(Token.NoToken, bound)) {
      Type = new MapType(finite, DafnyType.Int, type)
    };
    var sameBinderCopy = SubstituteKey(original, key, Integer(7));
    Assert.Same(original, sameBinderCopy.ProjectionFunctionsSource);

    var substituter = new Substituter(null, new Dictionary<IVariable, Expression>(),
      new Dictionary<TypeParameter, DafnyType> { [parameter] = DafnyType.Bool });
    var changed = Assert.IsType<MapComprehension>(substituter.Substitute(sameBinderCopy));

    Assert.Null(changed.ProjectionFunctionsSource);
    Assert.NotSame(original.BoundVars, changed.BoundVars);
    Assert.Same(DafnyType.Bool, Assert.Single(changed.BoundVars).Type);
    Assert.Same(changed.BoundVars[0], Assert.IsType<IdentifierExpr>(changed.Term).Var);
    Assert.Same(DafnyType.Bool, changed.Type.AsMapType.Range);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task RepeatedAndImportedTranslationsOwnTheirProjectionDeclarations(bool refresh) {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync("""
      module First {
        ghost function G<T(!new)>(value: T): map<int, T> {
          map x: T | x == value :: 0 := x
        }
      }
      module Second {
        import F = First
        ghost function H(value: bool): map<int, bool> {
          F.G(value)
        }
      }
      """, refresh);
    Assert.Equal(0, reporter.ErrorCount);

    // Resolution and translation have global scope state and are serialized by
    // the production verifier. Repeated generators must still own distinct
    // backend declarations for the same resolved source, including imports.
    var translations = BoogieGenerator.Translate(program, reporter).ToList();
    translations.AddRange(BoogieGenerator.Translate(program, reporter));
    Assert.Equal(4, translations.Count);
    var declarations = new HashSet<Bpl.Function>();
    foreach (var (_, backend) in translations) {
      var projections = backend.TopLevelDeclarations.OfType<Bpl.Function>()
        .Where(function => function.Name.StartsWith("map$project$")).ToList();
      Assert.NotEmpty(projections);
      Assert.All(projections, projection => Assert.True(declarations.Add(projection)));
      Assert.Equal(0, backend.Resolve(program.Options));
      Assert.Equal(0, backend.Typecheck(program.Options));
    }
  }
}
