// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

using Microsoft.Dafny;
using DafnyType = Microsoft.Dafny.Type;

namespace DafnyCore.Test.Resolver.Cardinality;

[Collection("Cardinality resolution")]
public class CardinalityProfileTests {
  [Theory]
  [InlineData("seq<V>", false)]
  [InlineData("set<V>", false)]
  [InlineData("multiset<V>", false)]
  [InlineData("map<V, int>", false)]
  [InlineData("map<int, V>", false)]
  [InlineData("iset<V>", true)]
  [InlineData("imap<V, int>", true)]
  [InlineData("imap<int, V>", false)]
  [InlineData("V -> bool", true)]
  [InlineData("V --> bool", true)]
  [InlineData("V ~> bool", true)]
  [InlineData("int -> V", false)]
  [InlineData("(V -> bool) -> bool", true)]
  [InlineData("(int, V)", false)]
  [InlineData("(int, V -> bool)", true)]
  [InlineData("array<V>", false)]
  [InlineData("array?<V>", false)]
  public async Task BuiltinPositionsMatchCardinalityDiscipline(string representation, bool expanding) {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync(
      $"trait V {{}} datatype Holder = Holder(ghost payload: {representation})");
    Assert.Equal(0, reporter.ErrorCount);
    var v = Find(program, "V");
    var holder = (DatatypeDecl)Find(program, "Holder");
    var payloadType = holder.Ctors.Single().Formals.Single().Type;
    var visitor = Visitor(program);
    var profile = visitor.Profile(new CardinalityTypeUse(payloadType), Reason(holder));
    Assert.True(profile.TryGet(CardinalityAtom.Head(v), out var dependency));
    Assert.Equal(expanding ? CardinalityWeight.Expanding : CardinalityWeight.Preserving, dependency.Weight);
  }

  [Theory]
  [InlineData(TPVarianceSyntax.NonVariant_Strict, false)]
  [InlineData(TPVarianceSyntax.Covariant_Strict, false)]
  [InlineData(TPVarianceSyntax.NonVariant_Permissive, true)]
  [InlineData(TPVarianceSyntax.Covariant_Permissive, true)]
  [InlineData(TPVarianceSyntax.Contravariance, true)]
  public void CardinalityModeDoesNotConfuseOrdinaryVariance(TPVarianceSyntax mode, bool expanding) {
    var parameter = new TypeParameter(Token.NoToken, new Name(Token.NoToken, "X"), mode);
    Assert.Equal(expanding ? CardinalityWeight.Expanding : CardinalityWeight.Preserving,
      CardinalityWeights.Mode(parameter));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task NominalProfileUsesAdvertisedModeEvenForPhantomFormal(bool permissive) {
    var (program, _) = await CardinalitySourceTests.ResolveAsync(
      $"trait V {{}} datatype Phantom<{(permissive ? "!" : "")}T> = Phantom");
    var v = Find(program, "V");
    var phantom = Find(program, "Phantom");
    var type = new UserDefinedType(phantom.Origin, phantom.Name, phantom,
      [new UserDefinedType(v.Origin, v.Name, v, [])]);
    var profile = Visitor(program).Profile(new CardinalityTypeUse(type), Reason(phantom));
    Assert.True(profile.TryGet(CardinalityAtom.Head(phantom), out var head));
    Assert.Equal(CardinalityWeight.Preserving, head.Weight);
    Assert.True(profile.TryGet(CardinalityAtom.Head(v), out var dependency));
    Assert.Equal(permissive ? CardinalityWeight.Expanding : CardinalityWeight.Preserving, dependency.Weight);
  }

  [Fact]
  public async Task AliasesKeepTheirHeadAndDoNotEagerlyUnfoldTheirPayload() {
    var (program, _) = await CardinalitySourceTests.ResolveAsync("trait V {} type F = V -> bool");
    var v = Find(program, "V");
    var alias = Find(program, "F");
    var aliasType = new UserDefinedType(alias.Origin, alias.Name, alias, []);
    var profile = Visitor(program).Profile(new CardinalityTypeUse(aliasType), Reason(alias));
    Assert.True(profile.TryGet(CardinalityAtom.Head(alias), out _));
    Assert.False(profile.TryGet(CardinalityAtom.Head(v), out _));
    var rhs = ((TypeSynonymDecl)alias).Rhs;
    var representation = Visitor(program).Profile(new CardinalityTypeUse(rhs), Reason(alias));
    Assert.True(representation.TryGet(CardinalityAtom.Head(v), out var dependency));
    Assert.Equal(CardinalityWeight.Expanding, dependency.Weight);
  }

  [Fact]
  public async Task ViewAlphaRenamingPreservesOwnerAndFormalPosition() {
    var (program, _) = await CardinalitySourceTests.ResolveAsync("type Original<T> type View<X> type Other<Y>");
    var original = Find(program, "Original");
    var view = Find(program, "View");
    var other = Find(program, "Other");
    view.CardinalityViewOf = original;
    var canonicalizer = new CardinalityCanonicalizer(CancellationToken.None);
    Assert.Same(original, canonicalizer.Declaration(view));
    Assert.Equal(CardinalityAtom.Formal(original, 0), canonicalizer.Formal(view.TypeArgs.Single()));
    Assert.NotEqual(canonicalizer.Formal(original.TypeArgs.Single()), canonicalizer.Formal(other.TypeArgs.Single()));
  }

  [Fact]
  public async Task InternalSynonymSubstitutesActualsBeforeProfiling() {
    var (program, _) = await CardinalitySourceTests.ResolveAsync("trait V {} type Original<!T>");
    var v = Find(program, "V");
    var original = (AbstractTypeDecl)Find(program, "Original");
    var internalView = original.SynonymInfo.SelfSynonymDecl;
    var actual = new UserDefinedType(v.Origin, v.Name, v, []);
    var type = new UserDefinedType(internalView.Origin, internalView.Name, internalView, [actual]);
    var profile = Visitor(program).Profile(new CardinalityTypeUse(type), Reason(original));
    Assert.True(profile.TryGet(CardinalityAtom.Head(original), out _));
    Assert.True(profile.TryGet(CardinalityAtom.Head(v), out var dependency));
    Assert.Equal(CardinalityWeight.Expanding, dependency.Weight);
    Assert.False(profile.TryGet(CardinalityAtom.Head(internalView), out _));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task InternalViewRequiresUnnestedPositionalFormalActuals(bool nestedView) {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync("datatype Box<T> = Box(x: T)");
    Assert.Equal(0, reporter.ErrorCount);
    var box = Find(program, "Box");
    var formal = new UserDefinedType(box.TypeArgs.Single());
    var rhs = new UserDefinedType(box.Origin, box.Name, box, [formal]);
    var view = new InternalTypeSynonymDecl(box.Origin, new Name(box.Origin, "Internal"),
      TypeParameter.GetExplicitCharacteristics(box), box.TypeArgs, box.EnclosingModuleDefinition, rhs, box.Attributes);
    if (nestedView) {
      // Box<Internal<T>> manufactures fresh substitution environments if a
      // generated view is incorrectly treated as an arbitrary user synonym.
      rhs.TypeArgs[0] = new UserDefinedType(view.Origin, view.Name, view, [formal]);
    } else {
      formal.TypeArgs.Add(DafnyType.Int);
    }
    Assert.Throws<CardinalityTypeException>(() =>
      new CardinalityCanonicalizer(CancellationToken.None).Declaration(view));
    var use = new UserDefinedType(view.Origin, view.Name, view, [DafnyType.Int]);
    Assert.Throws<CardinalityTypeException>(() => Visitor(program).Profile(new CardinalityTypeUse(use), Reason(box)));
  }

  [Fact]
  public async Task InternalViewsRejectCyclicActualsAndAcceptFiniteSharedActuals() {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync("type Id<T> = T");
    Assert.Equal(0, reporter.ErrorCount);
    var id = (TypeSynonymDecl)Find(program, "Id");
    var view = id.SynonymInfo.SelfSynonymDecl;
    var cyclic = new UserDefinedType(view.Origin, view.Name, view, [DafnyType.Int]);
    cyclic.TypeArgs[0] = cyclic;
    var visitor = Visitor(program);
    Assert.Throws<CardinalityTypeException>(() => visitor.Profile(new CardinalityTypeUse(cyclic), Reason(id)));
    Assert.Throws<CardinalityTypeException>(() => visitor.SameType(new CardinalityTypeUse(cyclic), new CardinalityTypeUse(cyclic)));
    Assert.Throws<CardinalityTypeException>(() => visitor.DirectRetainedFormal(new CardinalityTypeUse(cyclic)));

    var inner = new UserDefinedType(view.Origin, view.Name, view, [DafnyType.Int]);
    var outer = new UserDefinedType(view.Origin, view.Name, view, [inner]);
    var shared = new MapType(true, outer, outer);
    Assert.True(visitor.Profile(new CardinalityTypeUse(shared), Reason(id))
      .TryGet(CardinalityAtom.Head(id), out var dependency));
    Assert.Equal(CardinalityWeight.Preserving, dependency.Weight);
    var sibling = new UserDefinedType(view.Origin, view.Name, view, [inner]);
    Assert.True(visitor.SameType(new CardinalityTypeUse(outer), new CardinalityTypeUse(sibling)));

    var formal = new UserDefinedType(id.TypeArgs.Single());
    var retainedInner = new UserDefinedType(view.Origin, view.Name, view, [formal]);
    var retainedOuter = new UserDefinedType(view.Origin, view.Name, view, [retainedInner]);
    Assert.Equal(CardinalityAtom.Formal(id, 0), visitor.DirectRetainedFormal(new CardinalityTypeUse(retainedOuter)));
  }

  [Fact]
  public async Task SelectedReplacementJoinsExposedModesWithoutChangingSourceParameters() {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync("""
      trait V {}
      replaceable module A { type F<!X> }
      replaceable module B replaces A { type F<X> = X }
      module C replaces B {}
      """);
    Assert.Equal(0, reporter.ErrorCount);
    var original = program.RawModules().Single(module => module.Name == "A").TopLevelDecls.Single(declaration => declaration.Name == "F");
    var selected = program.RawModules().Single(module => module.Name == "C").TopLevelDecls.Single(declaration => declaration.Name == "F");
    var canonicalizer = new CardinalityCanonicalizer(CancellationToken.None, program.Replacements);
    Assert.Same(selected, canonicalizer.Declaration(original));
    Assert.Equal(CardinalityWeight.Expanding, Assert.Single(canonicalizer.AdvertisedModes(original)));
    Assert.Equal(CardinalityWeight.Expanding, Assert.Single(canonicalizer.AdvertisedModes(selected)));
    Assert.False(original.TypeArgs.Single().StrictVariance);
    Assert.True(selected.TypeArgs.Single().StrictVariance);
    Assert.Equal(CardinalityAtom.Formal(selected, 0), canonicalizer.Formal(original.TypeArgs.Single()));

    var v = Find(program, "V");
    var actual = new UserDefinedType(v.Origin, v.Name, v, []);
    var profile = Visitor(program).Profile(new CardinalityTypeUse(
      new UserDefinedType(selected.Origin, selected.Name, selected, [actual])), Reason(selected));
    Assert.True(profile.TryGet(CardinalityAtom.Head(selected), out _));
    Assert.True(profile.TryGet(CardinalityAtom.Head(v), out var dependency));
    Assert.Equal(CardinalityWeight.Expanding, dependency.Weight);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task MalformedReplacementCorrespondenceFailsClosed(bool weakenContract) {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync("""
      replaceable module A { type F<X> }
      module B replaces A { type F<X> = X }
      """);
    Assert.Equal(0, reporter.ErrorCount);
    var selected = program.RawModules().Single(module => module.Name == "B").TopLevelDecls.Single(declaration => declaration.Name == "F");
    if (weakenContract) {
      selected.TypeArgs.Single().VarianceSyntax = TPVarianceSyntax.NonVariant_Permissive;
    } else {
      selected.CardinalityRefinementBase = null;
    }
    Assert.Throws<CardinalityTypeException>(() =>
      new CardinalityCanonicalizer(CancellationToken.None, program.Replacements));
    Assert.False(CardinalityValidator.Validate(program, CancellationToken.None).Succeeded);
    Assert.Null(program.CardinalityValidationReceipt);
    Assert.Equal("r_cardinality_unclassified_type", Assert.Single(reporter.AllMessagesByLevel[ErrorLevel.Error]).ErrorId);
  }

  [Fact]
  public async Task NullableAndNonNullReferenceViewsKeepTheSameMode() {
    var (program, _) = await CardinalitySourceTests.ResolveAsync(
      "trait V {} class G<!T> { ghost var f: T -> bool } datatype Holder = Holder(g: G<V>, h: G?<V>)");
    var v = Find(program, "V");
    var g = Find(program, "G");
    var holder = (DatatypeDecl)Find(program, "Holder");
    foreach (var formal in holder.Ctors.Single().Formals) {
      var profile = Visitor(program).Profile(new CardinalityTypeUse(formal.Type), Reason(holder));
      Assert.True(profile.TryGet(CardinalityAtom.Head(g), out _));
      Assert.True(profile.TryGet(CardinalityAtom.Head(v), out var dependency));
      Assert.Equal(CardinalityWeight.Expanding, dependency.Weight);
    }
  }

  [Fact]
  public async Task UnknownFormsAndInvalidDescriptorsFailClosed() {
    var (program, _) = await CardinalitySourceTests.ResolveAsync("type D<T> type View<X, Y>");
    var declaration = Find(program, "D");
    var visitor = Visitor(program);
    var defaultClass = program.DefaultModuleDef.DefaultClass!;
    Assert.Throws<CardinalityTypeException>(() => visitor.Profile(
      new CardinalityTypeUse(new UserDefinedType(defaultClass.Origin, defaultClass.Name, defaultClass, [])), Reason(declaration)));
    Assert.Throws<CardinalityTypeException>(() => visitor.Profile(new CardinalityTypeUse(new UnknownType()), Reason(declaration)));
    Assert.Throws<CardinalityTypeException>(() => visitor.Profile(new CardinalityTypeUse(new InferredTypeProxy()), Reason(declaration)));
    Assert.Throws<CardinalityTypeException>(() => visitor.Profile(new CardinalityTypeUse(new SeqType(null)), Reason(declaration)));
    Assert.Throws<CardinalityTypeException>(() => visitor.Profile(
      new CardinalityTypeUse(new UserDefinedType(declaration.Origin, declaration.Name, declaration, [])), Reason(declaration)));
    Assert.Throws<CardinalityTypeException>(() => CardinalitySubstitution.Bind(declaration, []));
    var view = Find(program, "View");
    view.CardinalityViewOf = declaration;
    Assert.Throws<CardinalityTypeException>(() => new CardinalityCanonicalizer(CancellationToken.None).Declaration(view));
    declaration.CardinalityViewOf = declaration;
    Assert.Throws<CardinalityTypeException>(() => new CardinalityCanonicalizer(CancellationToken.None).Declaration(declaration));
  }

  [Fact]
  public async Task AtomicTypesRejectUnexpectedArgumentsBeforeSubstitution() {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync("type Owner<T>");
    Assert.Equal(0, reporter.ErrorCount);
    var owner = Find(program, "Owner");
    var malformedBasic = new BoolType { TypeArgs = [DafnyType.Int] };
    var malformedFormal = new UserDefinedType(owner.TypeArgs.Single());
    malformedFormal.TypeArgs.Add(DafnyType.Int);
    var substitution = CardinalitySubstitution.Bind(owner, [new CardinalityTypeUse(new BoolType())]);
    var visitor = Visitor(program);
    var uses = new[] {
      new CardinalityTypeUse(malformedBasic),
      new CardinalityTypeUse(malformedFormal),
      new CardinalityTypeUse(malformedFormal, substitution)
    };
    foreach (var use in uses) {
      Assert.Throws<CardinalityTypeException>(() => visitor.Normalize(use));
      Assert.Throws<CardinalityTypeException>(() => visitor.Profile(use, Reason(owner)));
      Assert.Throws<CardinalityTypeException>(() => visitor.DirectRetainedFormal(use));
    }
  }

  [Fact]
  public async Task ViewCannotWeakenTheCanonicalFormalContract() {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync("type Original<T> type View<!X>");
    Assert.Equal(0, reporter.ErrorCount);
    var original = Find(program, "Original");
    var view = Find(program, "View");
    view.CardinalityViewOf = original;
    Assert.Throws<CardinalityTypeException>(() =>
      new CardinalityCanonicalizer(CancellationToken.None).Declaration(view));
  }

  [Fact]
  public async Task UserDeclarationsCannotForgeBuiltinProfilesByName() {
    var (program, _) = await CardinalitySourceTests.ResolveAsync(
      "trait V {} type Fake<T, U> datatype Holder = Holder(f: V -> bool)");
    var v = Find(program, "V");
    var template = (AbstractTypeDecl)Find(program, "Fake");
    var fakeArrow = new AbstractTypeDecl(template.Origin,
      new Name(template.Origin, ArrowType.ArrowTypeName(1)), template.EnclosingModuleDefinition,
      template.Characteristics, template.TypeArgs, [], [], null, false);
    var visitor = Visitor(program);
    Assert.False(visitor.IsBuiltin(fakeArrow));
    var actual = new UserDefinedType(v.Origin, v.Name, v, []);
    var profile = visitor.Profile(new CardinalityTypeUse(
      new UserDefinedType(fakeArrow.Origin, fakeArrow.Name, fakeArrow, [actual, DafnyType.Bool])), Reason(fakeArrow));
    Assert.True(profile.TryGet(CardinalityAtom.Head(fakeArrow), out _));
    Assert.True(profile.TryGet(CardinalityAtom.Head(v), out var dependency));
    Assert.Equal(CardinalityWeight.Preserving, dependency.Weight);
  }

  [Fact]
  public async Task CyclicTypeCursorsFailWhileSharedSubexpressionsRemainLegal() {
    var (program, _) = await CardinalitySourceTests.ResolveAsync("trait V {} type Id<T> = T");
    var v = Find(program, "V");
    var shared = new UserDefinedType(v.Origin, v.Name, v, []);
    var visitor = Visitor(program);
    var valid = visitor.Profile(new CardinalityTypeUse(new MapType(true, shared, shared)), Reason(v));
    Assert.True(valid.TryGet(CardinalityAtom.Head(v), out _));
    var cycle = new SeqType(shared);
    cycle.TypeArgs[0] = cycle;
    Assert.Throws<CardinalityTypeException>(() => visitor.Profile(new CardinalityTypeUse(cycle), Reason(v)));
    var id = Find(program, "Id");
    var identityCycle = new UserDefinedType(id.Origin, id.Name, id, [shared]);
    identityCycle.TypeArgs[0] = identityCycle;
    Assert.Throws<CardinalityTypeException>(() => visitor.Profile(new CardinalityTypeUse(identityCycle), Reason(v)));
    Assert.Throws<CardinalityTypeException>(() => visitor.DirectRetainedFormal(new CardinalityTypeUse(identityCycle)));
  }

  [Fact]
  public async Task DeepProfilesAndCancellationUseExplicitTraversal() {
    var (program, _) = await CardinalitySourceTests.ResolveAsync("trait V {}");
    var v = Find(program, "V");
    DafnyType nested = new UserDefinedType(v.Origin, v.Name, v, []);
    for (var depth = 0; depth < 10000; depth++) {
      nested = new SeqType(nested);
    }
    var profile = Visitor(program).Profile(new CardinalityTypeUse(nested), Reason(v));
    Assert.True(profile.TryGet(CardinalityAtom.Head(v), out var dependency));
    Assert.Equal(CardinalityWeight.Preserving, dependency.Weight);
    using var cancellation = new CancellationTokenSource();
    await cancellation.CancelAsync();
    var visitor = new CardinalityTypeVisitor(program.SystemModuleManager,
      new CardinalityCanonicalizer(cancellation.Token), cancellation.Token);
    Assert.Throws<OperationCanceledException>(() => visitor.Profile(new CardinalityTypeUse(nested), Reason(v)));
  }

  internal static TopLevelDecl Find(Program program, string name) =>
    program.RawModules().SelectMany(module => module.TopLevelDecls).Single(declaration => declaration.Name == name);

  internal static CardinalityTypeVisitor Visitor(Program program) =>
    new(program.SystemModuleManager, new CardinalityCanonicalizer(CancellationToken.None, program.Replacements), CancellationToken.None);

  internal static CardinalityReason Reason(TopLevelDecl declaration) =>
    new(declaration.Origin, CardinalityReasonKind.ConstructorFormal, "payload");

  private sealed class UnknownType : DafnyType {
    public override string TypeName(DafnyOptions options, ModuleDefinition? context, bool parseAble = false) => "unknown";
    public override DafnyType Subst(IDictionary<TypeParameter, DafnyType> subst) => this;
    public override DafnyType ReplaceTypeArguments(List<DafnyType> arguments) => this;
    public override bool Equals(DafnyType that, bool keepConstraints = false) => ReferenceEquals(this, that);
    public override bool ComputeMayInvolveReferences(ISet<DatatypeDecl> visitedDatatypes, bool generalArrows = false) => false;
  }
}
