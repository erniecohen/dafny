using Microsoft.Dafny;
using Type = Microsoft.Dafny.Type;

namespace DafnyCore.Test;

public class NumericAncestryTests : NumericAncestryTestFixture {
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void FiniteAncestryIsIterative(bool real) {
    Type type = real ? Type.Real : Type.Int;
    for (var i = 0; i < 2048; i++) {
      type = Application(Newtype("N" + i, type));
    }
    AssertClassification(type, real ? Type.NumericAncestryKind.Real : Type.NumericAncestryKind.Integer);
    Assert.Same(real ? Type.Real : Type.Int, type.NormalizeToAncestorType());
  }

  [Fact]
  public void NonNumericScalarFamiliesStaySeparate() {
    foreach (var type in new Type[] { Type.Bool, Type.Char, Type.BigOrdinal, new BitvectorType(Token.NoToken, 8) }) {
      AssertClassification(type, Type.NumericAncestryKind.NonNumeric);
      Assert.Same(type, type.NormalizeToAncestorTypeChecked().AncestorType);
    }
  }



  [Fact]
  public void WalkStorageContainsNoResultsFromEarlierQueries() {
    var (type, baseDeclaration) = Chain("Mutable", Type.Int, 64);
    AssertClassification(type, Type.NumericAncestryKind.Integer);
    baseDeclaration.BaseType = Type.Real;
    AssertClassification(type, Type.NumericAncestryKind.Real);
    baseDeclaration.BaseType = type;
    AssertClassification(type, Type.NumericAncestryKind.Cyclic);
    baseDeclaration.BaseType = Type.Int;
    AssertClassification(type, Type.NumericAncestryKind.Integer);

    var t = Parameter("T");
    var generic = Newtype("G", new UserDefinedType(t), t);
    var repeated = Application(generic, Application(generic, Type.Real));
    AssertClassification(repeated, Type.NumericAncestryKind.Real);
    generic.BaseType = Application(generic, new SeqType(new UserDefinedType(t)));
    AssertClassification(repeated, Type.NumericAncestryKind.Cyclic);
  }


  [Fact]
  public void ConcurrentQueriesKeepTheirWalkStorageIndependent() {
    var integer = Chain("ConcurrentInteger", Type.Int, 128).Type;
    var real = Chain("ConcurrentReal", Type.Real, 128).Type;
    var t = Parameter("T");
    var generic = Newtype("ConcurrentG", new UserDefinedType(t), t);
    var repeated = Application(generic, Application(generic, Type.Int));
    var cyclic = Newtype("ConcurrentCycle", Type.Int);
    cyclic.BaseType = Application(cyclic);
    var cycleType = Application(cyclic);
    Parallel.For(0, 128, _ => {
      AssertClassification(integer, Type.NumericAncestryKind.Integer);
      AssertClassification(real, Type.NumericAncestryKind.Real);
      AssertClassification(repeated, Type.NumericAncestryKind.Integer);
      AssertClassification(cycleType, Type.NumericAncestryKind.Cyclic);
    });
  }

  [Fact]
  public void DirectCycleHasADeclarationWitness() {
    var declaration = Newtype("A", Type.Int);
    declaration.BaseType = Application(declaration);
    var type = Application(declaration);
    AssertClassification(type, Type.NumericAncestryKind.Cyclic);
    Assert.Equal(new RedirectingTypeDecl[] { declaration }, type.ClassifyNumericAncestry().Cycle);
    var ancestor = type.NormalizeToAncestorTypeChecked();
    Assert.Equal(Type.AncestorTypeKind.Cyclic, ancestor.Kind);
    Assert.Null(ancestor.AncestorType);
  }

  [Fact]
  public void MutualCycleTerminates() {
    var a = Newtype("A", Type.Int);
    var b = Newtype("B", Application(a));
    a.BaseType = Application(b);
    AssertClassification(Application(a), Type.NumericAncestryKind.Cyclic);
    AssertClassification(Application(b), Type.NumericAncestryKind.Cyclic);
    var cycle = Application(a).ClassifyNumericAncestry().Cycle;
    Assert.NotNull(cycle);
    Assert.Equal(2, cycle.Count);
    Assert.Contains(a, cycle);
    Assert.Contains(b, cycle);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void RepeatedGenericApplicationsCanBeAcyclic(bool real) {
    var parameter = Parameter("T");
    var declaration = Newtype("G", new UserDefinedType(parameter), parameter);
    Type type = real ? Type.Real : Type.Int;
    for (var i = 0; i < 20; i++) {
      type = Application(declaration, type);
    }
    AssertClassification(type, real ? Type.NumericAncestryKind.Real : Type.NumericAncestryKind.Integer);
    Assert.Same(real ? Type.Real : Type.Int, type.NormalizeToAncestorType());
  }

  [Fact]
  public void ArtificialNumericSupertypesUseInstantiatedCheckedAncestry() {
    var t = Parameter("T");
    var identity = Newtype("G", new UserDefinedType(t), t);
    Assert.True(Type.IsHeadSupertypeOf(new IntVarietiesSupertype(),
      Application(identity, Application(identity, Type.Int))));
    Assert.True(Type.IsHeadSupertypeOf(new RealVarietiesSupertype(), Application(identity, Type.Real)));
    Assert.False(Type.IsHeadSupertypeOf(new IntVarietiesSupertype(), Application(identity, Type.Real)));
    Assert.True(Type.IsHeadSupertypeOf(new IntVarietiesSupertype(), Type.BigOrdinal));
    Assert.True(Type.IsHeadSupertypeOf(new IntVarietiesSupertype(), new BitvectorType(Token.NoToken, 8)));
  }

  [Fact]
  public void AGenericCycleCanChangeArgumentsAtEveryStep() {
    var parameter = Parameter("T");
    var declaration = Newtype("C", Type.Int, parameter);
    declaration.BaseType = Application(declaration, new SeqType(new UserDefinedType(parameter)));
    var type = Application(declaration, Type.Int);
    AssertClassification(type, Type.NumericAncestryKind.Cyclic);
    Assert.Equal(new RedirectingTypeDecl[] { declaration }, type.ClassifyNumericAncestry().Cycle);
  }

  [Fact]
  public void MutualGenericCycleCanChangeArgumentsAtEveryStep() {
    var t = Parameter("T");
    var u = Parameter("U");
    var a = Newtype("A", Type.Int, t);
    var b = Newtype("B", Application(a, new SeqType(new UserDefinedType(u))), u);
    a.BaseType = Application(b, new SeqType(new UserDefinedType(t)));
    AssertClassification(Application(a, Type.Real), Type.NumericAncestryKind.Cyclic);
    AssertClassification(Application(b, Type.Int), Type.NumericAncestryKind.Cyclic);
    Assert.Equal(2, Application(a, Type.Real).ClassifyNumericAncestry().Cycle.Count);
  }

  [Fact]
  public void AnUnresolvedBaseIsNotACycleAndResultsAreQueryLocal() {
    var proxy = new InferredTypeProxy();
    var nextProxy = new InferredTypeProxy();
    var declaration = Newtype("A", proxy);
    var type = Application(declaration);
    AssertClassification(type, Type.NumericAncestryKind.Undetermined);
    Assert.Null(type.ClassifyNumericAncestry().Cycle);
    Assert.Equal(Type.AncestorTypeKind.Undetermined, type.NormalizeToAncestorTypeChecked().Kind);
    proxy.T = nextProxy;
    AssertClassification(type, Type.NumericAncestryKind.Undetermined);
    nextProxy.T = Type.Int;
    AssertClassification(type, Type.NumericAncestryKind.Integer);
    nextProxy.T = type;
    AssertClassification(type, Type.NumericAncestryKind.Cyclic);
  }

  [Fact]
  public void UnresolvedNominalHeadsAreUndetermined() {
    var type = new UserDefinedType(Token.NoToken, "Unresolved", null);
    AssertClassification(type, Type.NumericAncestryKind.Undetermined);
    Assert.Null(type.ClassifyNumericAncestry().Cycle);
  }

  [Fact]
  public void SubstitutionPreservesNonNumericBaseArguments() {
    var t = Parameter("T");
    var inner = Newtype("Inner", new UserDefinedType(t), t);
    var u = Parameter("U");
    var outer = Newtype("Outer", Application(inner, new SeqType(new UserDefinedType(u))), u);
    var type = Application(outer, Type.Real);
    AssertClassification(type, Type.NumericAncestryKind.NonNumeric);
    var ancestor = Assert.IsType<SeqType>(type.NormalizeToAncestorTypeChecked().AncestorType);
    Assert.Same(Type.Real, ancestor.Arg);
  }

  [Fact]
  public void HiddenBasesKeepTheirInstantiatedArguments() {
    var t = Parameter("T");
    var hidden = Newtype("Hidden", new UserDefinedType(t), t);
    var unused = Parameter("Unused");
    var u = Parameter("U");
    var outer = Newtype("Outer", Application(hidden, new UserDefinedType(u)), unused, u);
    var scope = new VisibilityScope("AncestryScope");
    hidden.AddVisibilityScope(scope, true);
    outer.AddVisibilityScope(scope, false);
    Type.PushScope(scope);
    Type.EnableScopes();
    try {
      var type = Application(outer, Type.Int, Type.Real);
      AssertClassification(type, Type.NumericAncestryKind.NonNumeric);
      var ancestor = Assert.IsType<UserDefinedType>(type.NormalizeToAncestorTypeChecked().AncestorType);
      Assert.Same(hidden.SynonymInfo.SelfSynonymDecl, ancestor.ResolvedClass);
      Assert.Equal(new Type[] { Type.Real }, ancestor.TypeArgs);
      hidden.AddVisibilityScope(scope, false);
      AssertClassification(type, Type.NumericAncestryKind.Real);
    } finally {
      Type.DisableScopes();
      Type.PopScope(scope);
    }
  }

  [Fact]
  public void HiddenCyclicDefinitionsAreOpaqueUntilRevealed() {
    var declaration = Newtype("HiddenCycle", Type.Int);
    declaration.BaseType = Application(declaration);
    declaration.IsCyclic = true;
    var scope = new VisibilityScope("OpaqueScope");
    declaration.AddVisibilityScope(scope, true);
    Type.PushScope(scope);
    Type.EnableScopes();
    try {
      AssertClassification(Application(declaration), Type.NumericAncestryKind.NonNumeric);
      declaration.AddVisibilityScope(scope, false);
      AssertClassification(Application(declaration), Type.NumericAncestryKind.Cyclic);
    } finally {
      Type.DisableScopes();
      Type.PopScope(scope);
    }
  }

  [Fact]
  public void FreshClonesDoNotInheritNewtypeCyclicity() {
    var proxy = new InferredTypeProxy();
    var variable = new BoundVar(Token.NoToken, "x", proxy);
    var declaration = new NewtypeDecl(SourceOrigin.NoToken, new Name("A"), [], module, variable,
      new LiteralExpr(Token.NoToken, true), SubsetTypeDecl.WKind.CompiledZero, null, [], [], null, false);
    proxy.T = Application(declaration);
    declaration.IsCyclic = true;
    // Type follows assigned proxies; UnnormalizedType is the storage shared with BaseType.
    Assert.Same(variable.UnnormalizedType, declaration.BaseType);
    Assert.Same(variable.Type, declaration.BaseType.Normalize());
    var clone = Assert.IsType<NewtypeDecl>(new Cloner().CloneDeclaration(declaration, module));
    Assert.False(clone.IsCyclic);
    Assert.Same(clone.Var.Type, clone.BaseType);
  }
}
