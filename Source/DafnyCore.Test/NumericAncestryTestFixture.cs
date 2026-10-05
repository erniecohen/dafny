using Microsoft.Dafny;
using Type = Microsoft.Dafny.Type;

namespace DafnyCore.Test;

public abstract class NumericAncestryTestFixture {
  protected readonly ModuleDefinition module = new(SourceOrigin.NoToken, new Name("AncestryTests"), [],
    ModuleKindEnum.Concrete, false, null, null, null);

  protected NewtypeDecl Newtype(string name, Type baseType, params TypeParameter[] parameters) {
    var declaration = new NewtypeDecl(SourceOrigin.NoToken, new Name(name), parameters.ToList(), module,
      baseType, SubsetTypeDecl.WKind.CompiledZero, null, [], [], null, false);
    for (var i = 0; i < parameters.Length; i++) {
      parameters[i].Parent = declaration;
      parameters[i].PositionalIndex = i;
    }
    return declaration;
  }

  protected static TypeParameter Parameter(string name) =>
    new(SourceOrigin.NoToken, new Name(name), TPVarianceSyntax.NonVariant_Strict);

  protected static UserDefinedType Application(NewtypeDecl declaration, params Type[] arguments) =>
    UserDefinedType.FromTopLevelDecl(SourceOrigin.NoToken, declaration, arguments.ToList());

  protected (Type Type, NewtypeDecl Base) Chain(string prefix, Type baseType, int count) {
    var declaration = Newtype(prefix + "0", baseType);
    Type type = Application(declaration);
    for (var i = 1; i < count; i++) {
      type = Application(Newtype(prefix + i, type));
    }
    return (type, declaration);
  }

  protected sealed class ThrowingSubstitutionType : Type {
    public override string TypeName(DafnyOptions options, ModuleDefinition context, bool parseAble = false) =>
      "throwing substitution";
    public override Type Subst(IDictionary<TypeParameter, Type> subst) =>
      throw new InvalidOperationException("Substitution failed");
    public override Type ReplaceTypeArguments(List<Type> arguments) => throw new NotSupportedException();
    public override bool Equals(Type that, bool keepConstraints = false) => ReferenceEquals(this, that);
    public override bool ComputeMayInvolveReferences(ISet<DatatypeDecl> visitedDatatypes, bool generalArrows = false) => false;
  }

  protected static void AssertClassification(Type type, Type.NumericAncestryKind expected) {
    Assert.Equal(expected, type.ClassifyNumericAncestry().Kind);
    Assert.Equal(expected is Type.NumericAncestryKind.Integer or Type.NumericAncestryKind.Real,
      type.IsNumericBased());
    Assert.Equal(expected == Type.NumericAncestryKind.Integer, type.IsNumericBased(Type.NumericPersuasion.Int));
    Assert.Equal(expected == Type.NumericAncestryKind.Real, type.IsNumericBased(Type.NumericPersuasion.Real));
  }

}
