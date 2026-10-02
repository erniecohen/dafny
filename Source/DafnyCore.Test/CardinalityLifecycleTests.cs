using System.Collections.Immutable;
using DafnyCore.Test.Resolver.Cardinality;
using Microsoft.Dafny;
using Type = Microsoft.Dafny.Type;

namespace DafnyCore.Test;

[Collection("Cardinality resolution")]
public class CardinalityLifecycleTests {
  private const string SafeProgram = "trait V {} datatype Box extends V = Box(value: int)";

  [Fact]
  public async Task CancelledResolutionClearsPreviousAdmission() {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync(SafeProgram);
    Assert.Equal(0, reporter.ErrorCount);
    Assert.True(program.CardinalityValidationReceipt?.Succeeded);

    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
      new ProgramResolver(program).Resolve(cancellation.Token));

    Assert.Null(program.CardinalityValidationReceipt);
    Assert.Throws<InvalidOperationException>(() => BoogieGenerator.Translate(program, reporter).ToList());
  }

  [Fact]
  public async Task CancelledValidationClearsPreviousAdmission() {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync(SafeProgram);
    Assert.Equal(0, reporter.ErrorCount);
    Assert.True(program.CardinalityValidationReceipt?.Succeeded);

    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    Assert.ThrowsAny<OperationCanceledException>(() => CardinalityValidator.Validate(program, cancellation.Token));

    Assert.Null(program.CardinalityValidationReceipt);
  }

  [Fact]
  public async Task CancellationDuringFailedValidationLeavesNoAdmission() {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync(SafeProgram);
    Assert.Equal(0, reporter.ErrorCount);
    Assert.True(program.CardinalityValidationReceipt?.Succeeded);
    var declarations = program.RawModules().SelectMany(module => module.TopLevelDecls).ToList();
    var parent = Assert.Single(declarations.OfType<TraitDecl>().Where(declaration => declaration.Name == "V"));
    var datatype = Assert.Single(declarations.OfType<IndDatatypeDecl>().Where(declaration => declaration.Name == "Box"));
    var constructor = Assert.Single(datatype.Ctors);
    var formal = Assert.Single(constructor.Formals);
    var parentType = UserDefinedType.FromTopLevelDecl(parent.Origin, parent);
    var expansiveType = new ArrowType(formal.Origin, program.SystemModuleManager.ArrowTypeDecls[1], [parentType], Type.Bool);
    constructor.Formals[0] = new Formal(formal.Origin, formal.Name, expansiveType, true, formal.IsGhost, null);

    using var cancellation = new CancellationTokenSource();
    var cancellingReporter = new CancellingCardinalityReporter(program.Options, cancellation);
    program.Reporter = cancellingReporter;
    Assert.ThrowsAny<OperationCanceledException>(() => CardinalityValidator.Validate(program, cancellation.Token));

    // Cancellation is triggered by a diagnostic from the validator, after its graph work begins.
    Assert.True(cancellation.IsCancellationRequested);
    Assert.Contains(cancellingReporter.AllMessagesByLevel[ErrorLevel.Error],
      diagnostic => diagnostic.ErrorId == "r_cardinality_expansive_cycle");
    Assert.Null(program.CardinalityValidationReceipt);
    Assert.Throws<InvalidOperationException>(() => BoogieGenerator.Translate(program, cancellingReporter).ToList());
  }

  [Fact]
  public async Task FailedRevalidationCannotReuseSuccessfulAdmission() {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync(SafeProgram);
    Assert.Equal(0, reporter.ErrorCount);
    Assert.True(program.CardinalityValidationReceipt?.Succeeded);

    var declarations = program.RawModules().SelectMany(module => module.TopLevelDecls).ToList();
    var parent = Assert.Single(declarations.OfType<TraitDecl>().Where(declaration => declaration.Name == "V"));
    var datatype = Assert.Single(declarations.OfType<IndDatatypeDecl>().Where(declaration => declaration.Name == "Box"));
    var constructor = Assert.Single(datatype.Ctors);
    var formal = Assert.Single(constructor.Formals);
    var parentType = UserDefinedType.FromTopLevelDecl(parent.Origin, parent);
    var expansiveType = new ArrowType(formal.Origin, program.SystemModuleManager.ArrowTypeDecls[1], [parentType], Type.Bool);
    constructor.Formals[0] = new Formal(formal.Origin, formal.Name, expansiveType, true, formal.IsGhost, null);

    var result = CardinalityValidator.Validate(program, CancellationToken.None);

    Assert.False(result.Succeeded);
    Assert.True(reporter.ErrorCount > 0);
    Assert.Null(program.CardinalityValidationReceipt);
    var generator = new BoogieGenerator(reporter, program.ProofDependencyManager);
    Assert.Throws<InvalidOperationException>(() => generator.DoTranslation(program, datatype.EnclosingModuleDefinition));
  }

  [Fact]
  public async Task TranslationRejectsUnvalidatedResolvedFixture() {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync(SafeProgram);
    Assert.Equal(0, reporter.ErrorCount);
    program.CardinalityValidationReceipt = null;
    var generator = new BoogieGenerator(reporter, program.ProofDependencyManager);

    var lowLevelError = Assert.Throws<InvalidOperationException>(() => generator.DoTranslation(program, program.DefaultModuleDef));
    Assert.Contains("cardinality validation", lowLevelError.Message);
    Assert.Throws<InvalidOperationException>(() => BoogieGenerator.Translate(program, reporter).ToList());

    // The supported low-level route computes admission; it cannot simply mark a fixture trusted.
    Assert.True(CardinalityValidator.Validate(program, CancellationToken.None).Succeeded);
    Assert.True(program.CardinalityValidationReceipt?.Succeeded);
  }

  [Fact]
  public async Task ProgramCloneDoesNotCopyAdmission() {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync(SafeProgram);
    Assert.Equal(0, reporter.ErrorCount);
    Assert.True(program.CardinalityValidationReceipt?.Succeeded);

    var clone = new Microsoft.Dafny.Program(new Cloner(cloneLiteralModuleDefinition: true), program);

    Assert.Null(clone.CardinalityValidationReceipt);
  }

  [Fact]
  public async Task VisibilityViewsKeepIdentityAndSemanticClonesDoNot() {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync(
      "trait V<X> {} datatype Box<X> extends V<X> = Box(value: X)");
    Assert.Equal(0, reporter.ErrorCount);
    var declarations = program.RawModules().SelectMany(module => module.TopLevelDecls).ToList();
    var parent = Assert.Single(declarations.OfType<TraitDecl>().Where(declaration => declaration.Name == "V"));
    var datatype = Assert.Single(declarations.OfType<IndDatatypeDecl>().Where(declaration => declaration.Name == "Box"));
    var obligation = UserDefinedType.FromTopLevelDecl(parent.Origin, parent, datatype.TypeArgs);
    datatype.CardinalityParentObligations = ImmutableArray.Create<Type>(obligation);

    Assert.DoesNotContain(datatype.Children, child => ReferenceEquals(child, obligation));

    var providedScope = new VisibilityScope();
    datatype.AddVisibilityScope(providedScope, true);
    var providedView = new ScopeCloner(providedScope).CloneDeclaration(datatype, datatype.EnclosingModuleDefinition);
    Assert.IsType<AbstractTypeDecl>(providedView);
    Assert.Same(datatype, providedView.CardinalityViewOf);
    Assert.Single(providedView.CardinalityParentObligations);

    var revealedView = new ScopeCloner(datatype.EnclosingModuleDefinition.VisibilityScope)
      .CloneDeclaration(datatype, datatype.EnclosingModuleDefinition);
    Assert.IsType<IndDatatypeDecl>(revealedView);
    Assert.Same(datatype, revealedView.CardinalityViewOf);

    var semanticClone = new Cloner().CloneDeclaration(providedView, datatype.EnclosingModuleDefinition);
    Assert.Null(semanticClone.CardinalityViewOf);
    Assert.Single(semanticClone.CardinalityParentObligations);
    Assert.NotSame(Assert.Single(providedView.CardinalityParentObligations),
      Assert.Single(semanticClone.CardinalityParentObligations));

    var resolvedClone = new Cloner(cloneResolvedFields: true)
      .CloneDeclaration(datatype, datatype.EnclosingModuleDefinition);
    var clonedParent = Assert.IsType<UserDefinedType>(Assert.Single(resolvedClone.CardinalityParentObligations));
    var clonedFormal = Assert.IsType<UserDefinedType>(Assert.Single(clonedParent.TypeArgs));
    Assert.Same(Assert.Single(resolvedClone.TypeArgs), clonedFormal.ResolvedClass);
  }

  [Fact]
  public async Task SynonymRefinementRetainsParentInTheRefiningParameterScope() {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync("""
      abstract module A {
        trait V<!X> {}
        type D<!X> extends V<X>
      }
      module B refines A { type D<!X> = X -> bool }
      """);
    Assert.Equal(0, reporter.ErrorCount);
    var refinedModule = Assert.Single(program.RawModules().Where(module => module.Name == "B"));
    var declaration = Assert.Single(refinedModule.TopLevelDecls.OfType<ConcreteTypeSynonymDecl>());
    var obligation = Assert.IsType<UserDefinedType>(Assert.Single(declaration.CardinalityParentObligations));
    var actual = Assert.IsType<UserDefinedType>(Assert.Single(obligation.TypeArgs));

    Assert.Same(Assert.Single(declaration.TypeArgs), actual.ResolvedClass);
    Assert.Same(declaration, Assert.Single(declaration.TypeArgs).Parent);
    Assert.Same(refinedModule, obligation.ResolvedClass.ViewAsClass.EnclosingModuleDefinition);
    Assert.Null(declaration.CardinalityViewOf);
    Assert.DoesNotContain(declaration.Children, child => ReferenceEquals(child, obligation));
    Assert.True(program.CardinalityValidationReceipt?.Succeeded);
  }

  [Fact]
  public async Task KindChangingRefinementCannotDropAParentCycle() {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync("""
      abstract module A {
        trait V {}
        type D extends V
      }
      module B refines A { type D = V -> bool }
      """);

    Assert.Contains(reporter.AllMessagesByLevel[ErrorLevel.Error],
      diagnostic => diagnostic.ErrorId == "r_cardinality_expansive_cycle");
    Assert.Null(program.CardinalityValidationReceipt);
    var refinedModule = Assert.Single(program.RawModules().Where(module => module.Name == "B"));
    var declaration = Assert.Single(refinedModule.TopLevelDecls.OfType<ConcreteTypeSynonymDecl>());
    Assert.Single(declaration.CardinalityParentObligations);
  }

  [Fact]
  public async Task SeparateRefinementsHaveSeparateCarrierIdentities() {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync("""
      abstract module A {
        trait V {}
        type D extends V
      }
      module B refines A { type D = int }
      module C refines A { type D = int -> V }
      """);
    Assert.Equal(0, reporter.ErrorCount);
    var b = Assert.Single(program.RawModules().Where(module => module.Name == "B"));
    var c = Assert.Single(program.RawModules().Where(module => module.Name == "C"));
    var bType = Assert.Single(b.TopLevelDecls.OfType<ConcreteTypeSynonymDecl>());
    var cType = Assert.Single(c.TopLevelDecls.OfType<ConcreteTypeSynonymDecl>());
    var bParent = Assert.IsType<UserDefinedType>(Assert.Single(bType.CardinalityParentObligations)).ResolvedClass.ViewAsClass;
    var cParent = Assert.IsType<UserDefinedType>(Assert.Single(cType.CardinalityParentObligations)).ResolvedClass.ViewAsClass;

    Assert.NotSame(bType, cType);
    Assert.NotSame(bParent, cParent);
    Assert.Same(b, bParent.EnclosingModuleDefinition);
    Assert.Same(c, cParent.EnclosingModuleDefinition);
    Assert.Null(bType.CardinalityViewOf);
    Assert.Null(cType.CardinalityViewOf);
    Assert.Null(bParent.CardinalityViewOf);
    Assert.Null(cParent.CardinalityViewOf);
    Assert.True(program.CardinalityValidationReceipt?.Succeeded);
  }

  [Fact]
  public async Task SelectedReplacementRecordsImmediateDeclarationIdentityWithoutMutatingModes() {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync("""
      replaceable module A { type F<!X> }
      replaceable module B replaces A { type F<X> = X }
      module C replaces B {}
      """);
    Assert.True(reporter.ErrorCount == 0, string.Join(Environment.NewLine, reporter.AllMessages.Select(message => message.Message)));
    var a = Assert.Single(program.RawModules().Where(module => module.Name == "A"));
    var b = Assert.Single(program.RawModules().Where(module => module.Name == "B"));
    var c = Assert.Single(program.RawModules().Where(module => module.Name == "C"));
    var aType = Assert.Single(a.TopLevelDecls.OfType<AbstractTypeDecl>());
    var bType = Assert.Single(b.TopLevelDecls.OfType<ConcreteTypeSynonymDecl>());
    var cType = Assert.Single(c.TopLevelDecls.OfType<ConcreteTypeSynonymDecl>());

    Assert.Same(aType, bType.CardinalityRefinementBase);
    Assert.Same(bType, cType.CardinalityRefinementBase);
    Assert.Same(c, program.Replacements[a]);
    Assert.Same(c, program.Replacements[b]);
    Assert.False(Assert.Single(aType.TypeArgs).StrictVariance);
    Assert.True(Assert.Single(bType.TypeArgs).StrictVariance);
    Assert.True(Assert.Single(cType.TypeArgs).StrictVariance);
    Assert.Null(bType.CardinalityViewOf);
    Assert.Null(cType.CardinalityViewOf);
    Assert.True(program.CardinalityValidationReceipt?.Succeeded);

    var semanticClone = new Cloner().CloneDeclaration(cType, c);
    Assert.Null(semanticClone.CardinalityRefinementBase);
    Assert.Null(semanticClone.CardinalityViewOf);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task IteratorRefinementRetainsParentObligationsInItsParameterScope(bool omitSignature) {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync($$"""
      abstract module A {
        trait V<T> {}
        type I<T> extends V<T>
      }
      module B refines A { iterator I<T>(x: T) {} }
      module C refines B { {{(omitSignature ? "iterator I ... {}" : "")}} }
      """);
    Assert.True(reporter.ErrorCount == 0, string.Join(Environment.NewLine, reporter.AllMessages.Select(message => message.Message)));
    var refinedModule = Assert.Single(program.RawModules().Where(module => module.Name == "C"));
    var iterator = Assert.Single(refinedModule.TopLevelDecls.OfType<IteratorDecl>());
    var obligation = Assert.IsType<UserDefinedType>(Assert.Single(iterator.CardinalityParentObligations));
    var actual = Assert.IsType<UserDefinedType>(Assert.Single(obligation.TypeArgs));

    Assert.Same(Assert.Single(iterator.TypeArgs), actual.ResolvedClass);
    Assert.Same(iterator, Assert.Single(iterator.TypeArgs).Parent);
    Assert.Same(refinedModule, obligation.ResolvedClass.ViewAsClass.EnclosingModuleDefinition);
    var previousModule = Assert.Single(program.RawModules().Where(module => module.Name == "B"));
    var previousIterator = Assert.Single(previousModule.TopLevelDecls.OfType<IteratorDecl>());
    Assert.Same(previousIterator, iterator.CardinalityRefinementBase);
    var input = Assert.Single(iterator.Ins);
    var capturedField = Assert.Single(iterator.Members.OfType<Field>().Where(field => field.Name == input.Name));
    Assert.Same(Assert.Single(iterator.TypeArgs), Assert.IsType<UserDefinedType>(input.Type.NormalizeExpand()).ResolvedClass);
    Assert.Same(Assert.Single(iterator.TypeArgs), Assert.IsType<UserDefinedType>(capturedField.Type.NormalizeExpand()).ResolvedClass);
    Assert.Same(iterator, capturedField.EnclosingClass);
    Assert.Empty(iterator.Traits);
    Assert.DoesNotContain(iterator.Children, child => ReferenceEquals(child, obligation));
    Assert.DoesNotContain(iterator.Children, child => ReferenceEquals(child, previousIterator));
    Assert.Null(iterator.CardinalityViewOf);
    Assert.True(program.CardinalityValidationReceipt?.Succeeded);
  }

  [Fact]
  public async Task HiddenReferenceViewKeepsCanonicalIdentity() {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync("class C<X> {}");
    Assert.Equal(0, reporter.ErrorCount);
    var declaration = Assert.Single(program.RawModules().SelectMany(module => module.TopLevelDecls)
      .OfType<ClassDecl>().Where(declaration => declaration.Name == "C"));
    var providedScope = new VisibilityScope();
    declaration.AddVisibilityScope(providedScope, true);
    declaration.NonNullTypeDecl!.AddVisibilityScope(providedScope, true);

    var view = new ScopeCloner(providedScope).CloneDeclaration(declaration, declaration.EnclosingModuleDefinition);

    Assert.IsType<AbstractTypeDecl>(view);
    Assert.Same(declaration, view.CardinalityViewOf);
    Assert.Equal(declaration.TypeArgs.Count, view.TypeArgs.Count);
  }

  private sealed class CancellingCardinalityReporter : BatchErrorReporter {
    private readonly CancellationTokenSource cancellation;

    public CancellingCardinalityReporter(DafnyOptions options, CancellationTokenSource cancellation) : base(options) {
      this.cancellation = cancellation;
    }

    public override bool MessageCore(DafnyDiagnostic diagnostic) {
      var reported = base.MessageCore(diagnostic);
      if (diagnostic.ErrorId == "r_cardinality_expansive_cycle") {
        cancellation.Cancel();
      }
      return reported;
    }
  }
}
