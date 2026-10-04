// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

using Microsoft.Dafny;
using Type = Microsoft.Dafny.Type;

namespace DafnyCore.Test;

[Collection("Cardinality resolution")]
public class NewtypeReferenceCharacteristicTests {
  private readonly ModuleDefinition module = new(SourceOrigin.NoToken, new Name("ReferenceCharacteristics"), [],
    ModuleKindEnum.Concrete, false, null, null, null);

  private NewtypeDecl Newtype(string name, Type baseType, params TypeParameter[] parameters) {
    var declaration = new NewtypeDecl(SourceOrigin.NoToken, new Name(name), parameters.ToList(), module,
      baseType, SubsetTypeDecl.WKind.CompiledZero, null, [], [], null, false);
    for (var i = 0; i < parameters.Length; i++) {
      parameters[i].Parent = declaration;
      parameters[i].PositionalIndex = i;
    }
    return declaration;
  }

  private static TypeParameter Parameter(string name) =>
    new(SourceOrigin.NoToken, new Name(name), TPVarianceSyntax.NonVariant_Strict);

  private static UserDefinedType Application(TopLevelDecl declaration, params Type[] arguments) =>
    UserDefinedType.FromTopLevelDecl(SourceOrigin.NoToken, declaration, arguments.ToList());

  private static SystemModuleManager System() =>
    new(new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null));

  private sealed class ModeProbe : Type {
    public bool? Mode;
    public ISet<DatatypeDecl>? Context;
    public override string TypeName(DafnyOptions options, ModuleDefinition context, bool parseAble = false) =>
      "mode probe";
    public override Type Subst(IDictionary<TypeParameter, Type> subst) => this;
    public override Type ReplaceTypeArguments(List<Type> arguments) => this;
    public override bool Equals(Type that, bool keepConstraints = false) => ReferenceEquals(this, that);
    public override bool ComputeMayInvolveReferences(ISet<DatatypeDecl> visitedDatatypes, bool generalArrows = false) {
      Mode = generalArrows;
      Context = visitedDatatypes;
      return generalArrows;
    }
  }

  private sealed class ThrowOnSubstitution : Type {
    public override string TypeName(DafnyOptions options, ModuleDefinition context, bool parseAble = false) =>
      "opaque base";
    public override Type Subst(IDictionary<TypeParameter, Type> subst) =>
      throw new InvalidOperationException("A hidden representation must not be instantiated");
    public override Type ReplaceTypeArguments(List<Type> arguments) => throw new NotSupportedException();
    public override bool Equals(Type that, bool keepConstraints = false) => ReferenceEquals(this, that);
    public override bool ComputeMayInvolveReferences(ISet<DatatypeDecl> visitedDatatypes, bool generalArrows = false) =>
      throw new InvalidOperationException("A hidden representation must not be classified");
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void ForwardingPreservesSelectorAndTraversalContext(bool generalArrows) {
    var probe = new ModeProbe();
    var wrapped = Application(Newtype("Outer", Application(Newtype("Inner", new SeqType(probe)))));
    var visited = new HashSet<DatatypeDecl>();
    Assert.Equal(generalArrows, wrapped.ComputeMayInvolveReferences(visited, generalArrows));
    Assert.Equal(generalArrows, probe.Mode);
    Assert.Same(visited, probe.Context);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void FiniteIdentityActualsRetainTheirOwnRepresentation(bool generalArrows) {
    var t = Parameter("T");
    var id = Newtype("Id", new UserDefinedType(t), t);
    var pure = Application(id, Application(id, Type.Int));
    Assert.False(pure.ComputeMayInvolveReferences(null, generalArrows));
    var reference = Application(System().ObjectDecl);
    var involving = Application(id, Application(id, new SeqType(reference)));
    Assert.True(involving.ComputeMayInvolveReferences(null, generalArrows));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void RepresentationUsesPermutedActualsAndIgnoresUnusedParameters(bool generalArrows) {
    var left = Parameter("Left");
    var right = Parameter("Right");
    var project = Newtype("Project", new SeqType(new UserDefinedType(right)), left, right);
    var reference = Application(System().ObjectDecl);
    Assert.False(Application(project, reference, Type.Int).ComputeMayInvolveReferences(null, generalArrows));
    Assert.True(Application(project, Type.Int, reference).ComputeMayInvolveReferences(null, generalArrows));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void RevisitedDatatypeStillChecksItsActualArguments(bool generalArrows) {
    var parameter = Parameter("T");
    var datatype = new IndDatatypeDecl(SourceOrigin.NoToken, new Name("D"), module, [parameter], [], [], [], null, true);
    parameter.Parent = datatype;
    parameter.PositionalIndex = 0;
    var t = Parameter("Actual");
    var id = Newtype("Id", new UserDefinedType(t), t);
    var visited = new HashSet<DatatypeDecl> { datatype };
    var pure = Application(id, Application(datatype, Type.Int));
    Assert.False(pure.ComputeMayInvolveReferences(visited, generalArrows));
    var reference = Application(System().ObjectDecl);
    var involving = Application(id, Application(datatype, reference));
    Assert.True(involving.ComputeMayInvolveReferences(visited, generalArrows));
  }

  [Fact]
  public void GeneralAndEmptyReadsArrowFamiliesRemainDistinct() {
    var system = System();
    var general = new ArrowType(SourceOrigin.NoToken, system.ArrowTypeDecls[1], [Type.Int], Type.Int);
    var partial = Application(system.PartialArrowTypeDecls[1], Type.Int, Type.Int);
    var total = Application(system.TotalArrowTypeDecls[1], Type.Int, Type.Int);
    var t = Parameter("T");
    var id = Newtype("Id", new UserDefinedType(t), t);
    var generalWrapped = Application(id, Application(id, general));
    Assert.False(generalWrapped.MayInvolveReferences);
    Assert.True(generalWrapped.MayShowReferences);
    foreach (var family in new Type[] { partial, total }) {
      var wrapped = Application(id, Application(id, family));
      Assert.True(family.Equals(wrapped.NormalizeToAncestorTypeChecked(preserveSubsetTypes: true).AncestorType, true));
      Assert.False(wrapped.MayInvolveReferences);
      Assert.False(wrapped.MayShowReferences);
      // The option is local: numeric/general ancestry callers still erase subsets by default.
      Assert.IsType<ArrowType>(wrapped.NormalizeToAncestorTypeChecked().AncestorType);
    }
  }

  [Fact]
  public void UnresolvedAncestryIsConservativeAndQueryLocal() {
    var proxy = new InferredTypeProxy();
    var declaration = Newtype("Unresolved", proxy);
    var wrapped = Application(declaration);
    Assert.True(wrapped.MayInvolveReferences);
    Assert.True(wrapped.MayShowReferences);
    proxy.T = Type.Int;
    Assert.False(wrapped.MayInvolveReferences);
    Assert.False(wrapped.MayShowReferences);
    declaration.BaseType = null;
    Assert.True(wrapped.MayInvolveReferences);
    Assert.True(wrapped.MayShowReferences);
    declaration.BaseType = Application(System().ObjectDecl);
    Assert.True(wrapped.MayInvolveReferences);
    Assert.True(wrapped.MayShowReferences);
  }

  [Fact]
  public void NewtypeCyclesAreConservativeWithoutPrimitiveFallback() {
    var a = Newtype("A", Type.Int);
    var b = Newtype("B", Application(a));
    a.BaseType = Application(b);
    var wrapped = Application(a);
    Assert.Equal(Type.AncestorTypeKind.Cyclic,
      wrapped.NormalizeToAncestorTypeChecked(preserveSubsetTypes: true).Kind);
    Assert.True(wrapped.MayInvolveReferences);
    Assert.True(wrapped.MayShowReferences);
  }

  [Fact]
  public void AnUnmarkedMixedSubsetCycleIsDetectedBeforeSubsetReentry() {
    var declaration = Newtype("N", Type.Int);
    var bound = new BoundVar(SourceOrigin.NoToken, "x", Application(declaration));
    var subset = new SubsetTypeDecl(SourceOrigin.NoToken, new Name("S"), TypeParameterCharacteristics.Default(), [],
      module, bound, new LiteralExpr(SourceOrigin.NoToken, true), SubsetTypeDecl.WKind.CompiledZero, null, null);
    declaration.BaseType = Application(subset);
    var wrapped = Application(declaration);
    // This assertion prevents recursive semantic classification if the stop guard regresses.
    Assert.Equal(Type.AncestorTypeKind.Cyclic,
      wrapped.NormalizeToAncestorTypeChecked(preserveSubsetTypes: true).Kind);
    Assert.True(wrapped.MayInvolveReferences);
    Assert.True(wrapped.MayShowReferences);
    Assert.Equal(Type.AncestorTypeKind.Cyclic,
      Application(subset).NormalizeToAncestorTypeChecked(preserveSubsetTypes: true).Kind);
    var actual = Parameter("Actual");
    var identity = Newtype("Identity", new UserDefinedType(actual), actual);
    var throughActual = Application(identity, Application(subset));
    Assert.Equal(Type.AncestorTypeKind.Cyclic, throughActual.NormalizeToAncestorTypeChecked().Kind);
    Assert.Equal(Type.AncestorTypeKind.Cyclic,
      throughActual.NormalizeToAncestorTypeChecked(preserveSubsetTypes: true).Kind);
  }

  [Fact]
  public void GenericProjectionIntoAnExpandingSubsetCycleIsConservative() {
    var element = Parameter("Element");
    var declaration = Newtype("Expanding", Type.Int, element);
    var selected = Parameter("Selected");
    var bound = new BoundVar(SourceOrigin.NoToken, "x", new UserDefinedType(selected));
    var subset = new SubsetTypeDecl(SourceOrigin.NoToken, new Name("Select"), TypeParameterCharacteristics.Default(),
      [selected], module, bound, new LiteralExpr(SourceOrigin.NoToken, true),
      SubsetTypeDecl.WKind.CompiledZero, null, null);
    selected.Parent = subset;
    selected.PositionalIndex = 0;
    declaration.BaseType = Application(subset, Application(declaration, new SeqType(new UserDefinedType(element))));
    var actual = Parameter("Actual");
    var identity = Newtype("Identity", new UserDefinedType(actual), actual);
    var wrapped = Application(identity, Application(declaration, Type.Int));
    Assert.Equal(Type.AncestorTypeKind.Cyclic, wrapped.NormalizeToAncestorTypeChecked().Kind);
    // First Identity's raw RHS is a parameter; Select's raw RHS is also a parameter.
    // The entered Expanding declaration supplies the cycle witness.
    Assert.Equal(Type.AncestorTypeKind.Cyclic,
      wrapped.NormalizeToAncestorTypeChecked(preserveSubsetTypes: true).Kind);
    Assert.True(wrapped.MayInvolveReferences);
    Assert.True(wrapped.MayShowReferences);
  }

  [Fact]
  public void AssignedProxyDoesNotHideAnUnmarkedSynonymCycle() {
    var rhs = new InferredTypeProxy();
    var synonym = new ConcreteTypeSynonymDecl(SourceOrigin.NoToken, new Name("Alias"),
      TypeParameterCharacteristics.Default(), [], module, rhs, null);
    rhs.T = Application(synonym);
    var baseProxy = new InferredTypeProxy { T = Application(synonym) };
    var wrapped = Application(Newtype("Outer", baseProxy));
    Assert.Equal(Type.AncestorTypeKind.Cyclic, wrapped.NormalizeToAncestorTypeChecked().Kind);
    Assert.Equal(Type.AncestorTypeKind.Cyclic,
      wrapped.NormalizeToAncestorTypeChecked(preserveSubsetTypes: true).Kind);
    Assert.True(wrapped.MayInvolveReferences);
    Assert.True(wrapped.MayShowReferences);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void ProvidedBaseUsesOnlyItsAdvertisedReferencePromise(bool noReferences) {
    var characteristics = TypeParameterCharacteristics.Default();
    characteristics.ContainsNoReferenceTypes = noReferences;
    var provided = new InternalTypeSynonymDecl(SourceOrigin.NoToken, new Name("Provided"),
      characteristics, [], module, new ThrowOnSubstitution(), null);
    var wrapped = Application(Newtype("Outer", Application(provided)));
    Assert.Equal(!noReferences, wrapped.MayInvolveReferences);
    Assert.Equal(!noReferences, wrapped.MayShowReferences);
  }

  [Fact]
  public void HiddenRepresentationsAreConservativeWithoutSubstitution() {
    var t = Parameter("T");
    var declaration = Newtype("Hidden", new ThrowOnSubstitution(), t);
    var scope = new VisibilityScope("OpaqueReferenceScope");
    declaration.AddVisibilityScope(scope, true);
    Type.PushScope(scope);
    Type.EnableScopes();
    try {
      var wrapped = Application(declaration, Type.Int);
      Assert.True(wrapped.MayInvolveReferences);
      Assert.True(wrapped.MayShowReferences);
    } finally {
      Type.DisableScopes();
      Type.PopScope(scope);
    }
  }

  [Fact]
  public void ScopeChangesDoNotReuseAReferenceClassification() {
    var declaration = Newtype("HiddenPure", Type.Int);
    var scope = new VisibilityScope("ReferenceScope");
    declaration.AddVisibilityScope(scope, true);
    Type.PushScope(scope);
    Type.EnableScopes();
    try {
      var wrapped = Application(declaration);
      Assert.True(wrapped.MayInvolveReferences);
      Assert.True(wrapped.MayShowReferences);
      declaration.AddVisibilityScope(scope, false);
      Assert.False(wrapped.MayInvolveReferences);
      Assert.False(wrapped.MayShowReferences);
    } finally {
      Type.DisableScopes();
      Type.PopScope(scope);
    }
  }
}
