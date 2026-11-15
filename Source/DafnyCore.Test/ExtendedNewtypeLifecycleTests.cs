// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

using Microsoft.Dafny;
using DafnyType = Microsoft.Dafny.Type;

namespace DafnyCore.Test;

[Collection("Cardinality resolution")]
public class ExtendedNewtypeLifecycleTests {
  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  public async Task MissingPrerequisitesAreResolverErrors(bool refresh, bool general) {
    var (_, reporter) = await ResolveAsync("newtype N = ORDINAL", true, refresh, general);
    Assert.Contains(reporter.AllMessagesByLevel[ErrorLevel.Error], diagnostic =>
      diagnostic.Message.Contains("--extended-newtype-bases requires --general-newtypes and --type-system-refresh"));
  }

  [Theory]
  [InlineData("newtype N = ORDINAL", true, DafnyType.AutoInitInfo.CompilableValue)]
  [InlineData("newtype N = o: ORDINAL | true witness 0", false, DafnyType.AutoInitInfo.CompilableValue)]
  [InlineData("newtype N = o: ORDINAL | true ghost witness 0", false, DafnyType.AutoInitInfo.Nonempty)]
  [InlineData("newtype N = ORDINAL witness *", false, DafnyType.AutoInitInfo.MaybeEmpty)]
  public async Task SemanticAndResolvedClonesHaveDistinctMetadataPolicies(
    string source, bool inheritsDefault, DafnyType.AutoInitInfo autoInit) {
    var (program, reporter) = await ResolveAsync(source);
    Assert.True(reporter.ErrorCount == 0, Diagnostics(reporter));
    var original = FindN(program);
    Assert.True(original.UseBaseReferenceCharacteristics);
    Assert.Equal(inheritsDefault, original.InheritsBaseDefault);
    Assert.Equal(autoInit, UserDefinedType.FromTopLevelDecl(original.Origin, original).GetAutoInit());

    var semantic = Assert.IsType<NewtypeDecl>(new Cloner()
      .CloneDeclaration(original, original.EnclosingModuleDefinition));
    Assert.False(semantic.UseBaseReferenceCharacteristics);
    Assert.False(semantic.InheritsBaseDefault);

    // Full Program cloning with CloneResolvedFields is unsupported; declaration
    // cloning is the API used to preserve unchanged resolved metadata.
    var resolved = Assert.IsType<NewtypeDecl>(new Cloner(cloneResolvedFields: true)
      .CloneDeclaration(original, original.EnclosingModuleDefinition));
    Assert.Equal(original.UseBaseReferenceCharacteristics, resolved.UseBaseReferenceCharacteristics);
    Assert.Equal(original.InheritsBaseDefault, resolved.InheritsBaseDefault);
    Assert.Equal(original.WitnessKind, resolved.WitnessKind);

    var (freshProgram, freshReporter) = await ResolveAsync(source);
    Assert.True(freshReporter.ErrorCount == 0, Diagnostics(freshReporter));
    var fresh = FindN(freshProgram);
    Assert.NotSame(original, fresh);
    Assert.Equal(fresh.UseBaseReferenceCharacteristics, resolved.UseBaseReferenceCharacteristics);
    Assert.Equal(fresh.InheritsBaseDefault, resolved.InheritsBaseDefault);
    Assert.Equal(autoInit, UserDefinedType.FromTopLevelDecl(fresh.Origin, fresh).GetAutoInit());
  }

  [Fact]
  public async Task FreshResolutionDoesNotReuseEnabledMetadataOrWitnessDecisions() {
    foreach (var enabled in new[] { true, false, true, false }) {
      var (program, reporter) = await ResolveAsync("newtype N = int", enabled);
      Assert.True(reporter.ErrorCount == 0, Diagnostics(reporter));
      var declaration = FindN(program);
      Assert.Equal(enabled, declaration.UseBaseReferenceCharacteristics);
      Assert.Equal(enabled, declaration.InheritsBaseDefault);
    }
    foreach (var source in new[] {
      "newtype N = ORDINAL", "newtype N = ORDINAL witness *", "newtype N = ORDINAL"
    }) {
      var (program, reporter) = await ResolveAsync(source);
      Assert.True(reporter.ErrorCount == 0, Diagnostics(reporter));
      var declaration = FindN(program);
      Assert.Equal(!source.Contains("witness *"), declaration.InheritsBaseDefault);
    }
    var (_, rejected) = await ResolveAsync("newtype N = ORDINAL", false);
    Assert.NotEmpty(rejected.AllMessagesByLevel[ErrorLevel.Error]);
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, true)]
  [InlineData(true, false)]
  public void ManifestRoundTripEnforcesLibraryImpliesClient(bool libraryEnabled, bool clientEnabled) {
    var libraryOptions = Options(libraryEnabled);
    var manifest = new DooFile.ManifestData(libraryOptions);
    Assert.Contains(CommonOptionBag.ExtendedNewtypeBases, OptionRegistry.GlobalOptions);
    Assert.Equal(libraryEnabled, Assert.IsType<bool>(manifest.Options[CommonOptionBag.ExtendedNewtypeBases.Name]));
    using var text = new StringWriter();
    manifest.Write(text);
    var loaded = DooFile.ManifestData.Read(new StringReader(text.ToString()));
    var clientOptions = Options(clientEnabled);
    var reporter = new BatchErrorReporter(clientOptions);
    var accepted = DooFile.CheckAndGetLibraryOptions(reporter,
      new Uri("file:///issue113-library.doo"), clientOptions, Token.Cli, loaded.Options);
    if (libraryEnabled && !clientEnabled) {
      Assert.Null(accepted);
      Assert.Contains(reporter.AllMessagesByLevel[ErrorLevel.Error], diagnostic =>
        diagnostic.ErrorId == "LibraryImpliesLocalOption" &&
        diagnostic.Message.Contains("--extended-newtype-bases"));
    } else {
      Assert.NotNull(accepted);
      Assert.Equal(0, reporter.ErrorCount);
      Assert.Equal(libraryEnabled, accepted!.Get(CommonOptionBag.ExtendedNewtypeBases));
    }
    Assert.Equal(clientEnabled, clientOptions.Get(CommonOptionBag.ExtendedNewtypeBases));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void AbsentOldManifestOptionMeansDisabled(bool clientEnabled) {
    var manifest = new DooFile.ManifestData(Options(false));
    manifest.Options.Remove(CommonOptionBag.ExtendedNewtypeBases.Name);
    var options = Options(clientEnabled);
    var reporter = new BatchErrorReporter(options);
    var accepted = DooFile.CheckAndGetLibraryOptions(reporter,
      new Uri("file:///issue113-old-library.doo"), options, Token.Cli, manifest.Options);
    Assert.NotNull(accepted);
    Assert.Equal(0, reporter.ErrorCount);
    Assert.False(accepted!.Get(CommonOptionBag.ExtendedNewtypeBases));
  }

  private static DafnyOptions Options(bool enabled, bool refresh = true, bool general = true) {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.TypeSystemRefresh, refresh);
    options.Set(CommonOptionBag.GeneralNewtypes, general);
    options.Set(CommonOptionBag.ExtendedNewtypeBases, enabled);
    return options;
  }

  private static async Task<(Program Program, BatchErrorReporter Reporter)> ResolveAsync(
    string source, bool enabled = true, bool refresh = true, bool general = true) {
    DafnyType.ResetScopes();
    var reporter = new BatchErrorReporter(Options(enabled, refresh, general));
    var parsed = await ProgramParser.Parse(source, new Uri("untitled:issue113-lifecycle.dfy"), reporter);
    Assert.True(reporter.ErrorCount == 0, Diagnostics(reporter));
    await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    return (parsed.Program, reporter);
  }

  private static NewtypeDecl FindN(Program program) => Assert.Single(program.RawModules()
    .SelectMany(module => module.TopLevelDecls).OfType<NewtypeDecl>().Where(declaration => declaration.Name == "N"));

  private static string Diagnostics(BatchErrorReporter reporter) =>
    string.Join(Environment.NewLine, reporter.AllMessages.Select(diagnostic => diagnostic.Message));
}
