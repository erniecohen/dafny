using System.Collections.Immutable;
using System.Reactive.Linq;
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

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task TranslatorDiagnosticsDoNotInvalidateAdmissionBetweenModules(bool observableReporter) {
    var (program, batchReporter) = await CardinalitySourceTests.ResolveAsync("""
      module First {
        ghost function f(x: int): int {
          if x <= 0 then 0 else 1 + f(x - 1)
        }
        method M(x: int) {
          assert {:fuel f, 0, 0} f(x) == 0;
        }
      }
      module Second {
        ghost function f(x: int): int {
          if x <= 0 then 0 else 1 + f(x - 1)
        }
        method M(x: int) {
          assert {:fuel f, 0, 0} f(x) == 0;
        }
      }
      """);
    Assert.Equal(0, batchReporter.ErrorCount);
    Assert.True(program.CardinalityValidationReceipt?.Succeeded);
    ErrorReporter reporter = observableReporter
      ? new ObservableErrorReporter(program.Options, new Uri("untitled:cardinality.dfy"))
      : batchReporter;
    program.Reporter = reporter;
    var diagnostics = new List<DafnyDiagnostic>();
    using var subscription = (reporter as ObservableErrorReporter)?.Updates.Subscribe(update => diagnostics.Add(update.Diagnostic));

    using var translations = BoogieGenerator.Translate(program, reporter).GetEnumerator();
    Assert.True(translations.MoveNext());
    Assert.Equal(1, reporter.ErrorCount);
    Assert.Equal(1, reporter.CountExceptVerifierAndCompiler(ErrorLevel.Error));
    Assert.Equal(0, reporter.CountExceptTranslatorVerifierAndCompiler(ErrorLevel.Error));
    Assert.True(translations.MoveNext());
    Assert.False(translations.MoveNext());
    Assert.Equal(2, reporter.ErrorCount);
    Assert.All(observableReporter ? diagnostics.Where(diagnostic => diagnostic.Level == ErrorLevel.Error) : batchReporter.AllMessagesByLevel[ErrorLevel.Error],
      diagnostic => Assert.Equal("g_fuel_must_increase", diagnostic.ErrorId));
    Assert.True(program.CardinalityValidationReceipt?.Succeeded);

    // An admission error still blocks both entry points even with an earlier successful receipt.
    reporter.Error(MessageSource.Resolver, "r_cardinality_unclassified_type", Token.NoToken, "test admission error");
    Assert.Equal(1, reporter.CountExceptTranslatorVerifierAndCompiler(ErrorLevel.Error));
    Assert.Throws<InvalidOperationException>(() => BoogieGenerator.Translate(program, reporter).ToList());
    var generator = new BoogieGenerator(reporter, program.ProofDependencyManager);
    Assert.Throws<InvalidOperationException>(() => generator.DoTranslation(program, program.DefaultModuleDef));
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

  [Fact]
  public async Task SelectedFacadesKeepIndependentImportIdentities() {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync("""
      abstract module A {
        trait {:termination false} V {}
        type F
      }
      module B1 refines A { type F = int }
      module B2 refines A { type F = bool }
      replaceable module P1 { import M : A export provides M }
      replaceable module P2 { import M : A export provides M }
      module C1 replaces P1 { import M = B1 }
      module C2 replaces P2 { import M = B2 }
      """);
    Assert.True(reporter.ErrorCount == 0, string.Join(Environment.NewLine, reporter.AllMessages.Select(message => message.Message)));
    var modules = program.RawModules().ToList();
    var a = Assert.Single(modules.Where(module => module.Name == "A"));
    var b1 = Assert.Single(modules.Where(module => module.Name == "B1"));
    var b2 = Assert.Single(modules.Where(module => module.Name == "B2"));
    var p1 = Assert.Single(modules.Where(module => module.Name == "P1"));
    var p2 = Assert.Single(modules.Where(module => module.Name == "P2"));
    var c1 = Assert.Single(modules.Where(module => module.Name == "C1"));
    var c2 = Assert.Single(modules.Where(module => module.Name == "C2"));
    var aType = Assert.Single(a.TopLevelDecls.OfType<AbstractTypeDecl>());
    var b1Type = Assert.Single(b1.TopLevelDecls.OfType<ConcreteTypeSynonymDecl>());
    var b2Type = Assert.Single(b2.TopLevelDecls.OfType<ConcreteTypeSynonymDecl>());
    var p1Import = Assert.Single(p1.TopLevelDecls.OfType<AbstractModuleDecl>());
    var p2Import = Assert.Single(p2.TopLevelDecls.OfType<AbstractModuleDecl>());
    var c1Import = Assert.Single(c1.TopLevelDecls.OfType<AliasModuleDecl>());
    var c2Import = Assert.Single(c2.TopLevelDecls.OfType<AliasModuleDecl>());
    var p1Type = Assert.IsType<AbstractTypeDecl>(p1Import.Signature.TopLevels["F"]);
    var p2Type = Assert.IsType<AbstractTypeDecl>(p2Import.Signature.TopLevels["F"]);
    var p1Synonym = p1Type.SelfSynonymDecl();
    var p2Synonym = p2Type.SelfSynonymDecl();

    Assert.Same(p1Import, c1Import.CardinalityRefinementBase);
    Assert.Same(p2Import, c2Import.CardinalityRefinementBase);
    Assert.NotSame(p1Type, p2Type);
    Assert.NotSame(aType, p1Type);
    Assert.NotSame(aType, p2Type);
    Assert.Same(aType, p1Type.CardinalityViewOf);
    Assert.Same(aType, p2Type.CardinalityViewOf);
    Assert.Same(p1Type, Assert.IsType<UserDefinedType>(p1Synonym.Rhs).ResolvedClass);
    Assert.Same(p2Type, Assert.IsType<UserDefinedType>(p2Synonym.Rhs).ResolvedClass);
    Assert.True(p1Type.EnclosingModuleDefinition.IsFacade);
    Assert.True(p2Type.EnclosingModuleDefinition.IsFacade);

    var providedView = Assert.Single(Assert.Single(p1.TopLevelDecls.OfType<ModuleExportDecl>())
      .EffectiveModule.TopLevelDecls.OfType<AbstractModuleDecl>());
    Assert.Same(p1Import, providedView.CardinalityViewOf);
    Assert.Same(p1Import.Signature, providedView.Signature);
    Assert.Null(providedView.CardinalityRefinementBase);

    var canonicalizer = new CardinalityCanonicalizer(CancellationToken.None, program.Replacements);
    Assert.Same(b1Type, canonicalizer.Declaration(p1Type));
    Assert.Same(b2Type, canonicalizer.Declaration(p2Type));
    Assert.Same(b1Type, canonicalizer.Declaration(p1Synonym));
    Assert.Same(b2Type, canonicalizer.Declaration(p2Synonym));
    Assert.Same(aType, canonicalizer.Declaration(aType));
    Assert.Same(aType, canonicalizer.Declaration(aType.SelfSynonymDecl()));
    Assert.NotSame(canonicalizer.Declaration(p1Type), canonicalizer.Declaration(p2Type));
    Assert.True(program.CardinalityValidationReceipt?.Succeeded);
  }

  [Fact]
  public async Task SelectedNestedFacadeRetainsExactModuleViewCorrespondence() {
    var (program, reporter) = await CardinalitySourceTests.ResolveAsync("""
      abstract module Leaf { type F }
      abstract module Template { import N : Leaf }
      module LeafImpl refines Leaf { type F = int }
      module Impl refines Template { import N = LeafImpl }
      replaceable module P { import M : Template export provides M }
      module C replaces P { import M = Impl }
      """);
    Assert.True(reporter.ErrorCount == 0, string.Join(Environment.NewLine, reporter.AllMessages.Select(message => message.Message)));
    var modules = program.RawModules().ToList();
    var leaf = Assert.Single(modules.Where(module => module.Name == "Leaf"));
    var template = Assert.Single(modules.Where(module => module.Name == "Template"));
    var implementation = Assert.Single(modules.Where(module => module.Name == "Impl"));
    var leafImplementation = Assert.Single(modules.Where(module => module.Name == "LeafImpl"));
    var placeholder = Assert.Single(modules.Where(module => module.Name == "P"));
    var replacement = Assert.Single(modules.Where(module => module.Name == "C"));
    var templateImport = Assert.Single(template.TopLevelDecls.OfType<AbstractModuleDecl>());
    var implementationImport = Assert.Single(implementation.TopLevelDecls.OfType<AliasModuleDecl>());
    var placeholderImport = Assert.Single(placeholder.TopLevelDecls.OfType<AbstractModuleDecl>());
    var replacementImport = Assert.Single(replacement.TopLevelDecls.OfType<AliasModuleDecl>());
    var nestedView = Assert.IsType<AbstractModuleDecl>(placeholderImport.Signature.TopLevels["N"]);
    var originalType = Assert.Single(leaf.TopLevelDecls.OfType<AbstractTypeDecl>());
    var selectedType = Assert.Single(leafImplementation.TopLevelDecls.OfType<ConcreteTypeSynonymDecl>());
    var nestedType = Assert.IsType<AbstractTypeDecl>(nestedView.Signature.TopLevels["F"]);

    Assert.Same(placeholderImport, replacementImport.CardinalityRefinementBase);
    Assert.Same(templateImport, implementationImport.CardinalityRefinementBase);
    Assert.Same(templateImport, nestedView.CardinalityViewOf);
    Assert.Null(nestedView.CardinalityRefinementBase);
    Assert.NotSame(templateImport.Signature.ModuleDef, nestedView.Signature.ModuleDef);
    Assert.Same(originalType, nestedType.CardinalityViewOf);
    Assert.Same(nestedType, Assert.IsType<UserDefinedType>(nestedType.SelfSynonymDecl().Rhs).ResolvedClass);

    var canonicalizer = new CardinalityCanonicalizer(CancellationToken.None, program.Replacements);
    Assert.Same(selectedType, canonicalizer.Declaration(nestedType));
    Assert.Same(selectedType, canonicalizer.Declaration(nestedType.SelfSynonymDecl()));
    Assert.Same(originalType, canonicalizer.Declaration(originalType));
    Assert.True(program.CardinalityValidationReceipt?.Succeeded);
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
