using Microsoft.Dafny;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DafnyCore.Test;

[CollectionDefinition("Modern B3 Core", DisableParallelization = true)]
public sealed class ModernB3CoreCollection { }

[Collection("Modern B3 Core")]
public class ModernB3InputAndChildTest {
  [Fact]
  public async Task PublicPreparedRootSeamSnapshotsCallerListBeforeStart() {
    var options = Options();
    var uri = new Uri("file:///prepared-modern-root.dfy");
    var fileSystem = new InMemoryFileSystem(new Dictionary<Uri, string> { [uri] = "method M() { }" });
    var reporter = new BatchErrorReporter(options);
    var root = DafnyFile.HandleDafnyFile(fileSystem, reporter, options, uri, Token.Cli)!;
    var roots = new List<DafnyFile> { root };
    var project = new DafnyProject(null, uri, null, new HashSet<string> { uri.LocalPath });
    var loader = new Loader();
    using var backend = new Backend();
    using var compilation = new Compilation(NullLogger<Compilation>.Instance, fileSystem, loader,
      new Verifier(), backend, new CompilationInput(options, 0, project) { PreparedRootFiles = roots });
    roots.Clear();
    options.CliRootSourceUris.Add(new Uri("file:///must-not-be-read.dfy"));
    compilation.Start();
    Assert.Same(root, Assert.Single(await compilation.RootFiles));
    var resolution = await compilation.Resolution;
    Assert.NotNull(resolution);
    Assert.Same(root, Assert.Single(loader.ParsedRoots!));
    Assert.Equal(1, loader.ParseCalls);
    Assert.Equal(0, reporter.ErrorCount);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task MissingResolutionOrScopeInventoryCannotBecomeEmptyPreparation(bool missingResolution) {
    var options = Options();
    var uri = new Uri("file:///prepared-modern-root.dfy");
    var fileSystem = new InMemoryFileSystem(new Dictionary<Uri, string> { [uri] = "method M() { }" });
    var root = DafnyFile.HandleDafnyFile(fileSystem, new BatchErrorReporter(options), options, uri, Token.Cli)!;
    var project = new DafnyProject(null, uri, null, new HashSet<string> { uri.LocalPath });
    var loader = new Loader { MissingResolution = missingResolution, MissingScope = !missingResolution };
    var verifier = new Verifier();
    using var backend = new Backend();
    using var compilation = new Compilation(NullLogger<Compilation>.Instance, fileSystem, loader,
      verifier, backend, new CompilationInput(options, 0, project) { PreparedRootFiles = new[] { root } });
    compilation.Start();
    await compilation.Resolution;
    var program = await compilation.ParsedProgram;
    Assert.NotNull(program);
    await Assert.ThrowsAsync<InvalidOperationException>(() =>
      compilation.PrepareModuleForCompilationAsync(program!.DefaultModuleDef, CancellationToken.None));
    Assert.Equal(0, verifier.Calls);
  }

  [Fact]
  public void LibraryChildForwardsFiniteB3ConfigurationAndSeparatesProgramArguments() {
    var options = Options();
    options.TimeLimit = 23;
    options.ResourceLimit = 456;
    options.VcsCores = 2;
    options.Set(BoogieOptionBag.ArithmeticSolver, 2);
    options.Set(B3OptionBag.Worker, new FileInfo("worker path.dll"));
    options.Set(BoogieOptionBag.SolverPath, new FileInfo("solver path"));
    options.ProverOptions.Add("O:must-not-become-a-child-flag=true");
    options.MainArgs = new List<string> { "--verification-backend", "boogie", "with spaces" };
    var args = ExecutableBackend.B3DafnyChildArguments(options, "program.doo", true);
    Assert.Equal(new[] { "--verification-backend", "b3", "--b3-worker", options.Get(B3OptionBag.Worker).FullName,
      "--solver-path", options.Get(BoogieOptionBag.SolverPath).FullName, "--verification-time-limit=23",
      "--resource-limit=456", "--cores=2", "--arithmetic-solver=2", "program.doo", "--",
      "--verification-backend", "boogie", "with spaces" }, args);
    Assert.DoesNotContain(args, argument => argument.Contains("must-not-become", StringComparison.Ordinal));
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public void ChildSkipPreservesBothParentAndCallerProvenance(bool parentSkip, bool verifyChild) {
    var options = Options();
    options.Set(BoogieOptionBag.NoVerify, parentSkip);
    var args = ExecutableBackend.B3DafnyChildArguments(options, "program.doo", verifyChild);
    Assert.Equal(parentSkip || !verifyChild, args.Contains("--no-verify"));
    Assert.DoesNotContain("--hidden-no-verify", args);
    Assert.Equal("b3", args[1]);
  }

  [Fact]
  public void EffectiveLegacySolverPathUsesTheLastRecordedValueWithoutForwardingOtherStrings() {
    var options = Options();
    options.ProverOptions.Add("PROVER_PATH=old");
    options.ProverOptions.Add("PROVER_PATH=effective");
    options.ProverOptions.Add("O:arbitrary=true");
    var args = ExecutableBackend.B3DafnyChildArguments(options, "program.doo", true);
    var index = args.ToList().IndexOf("--solver-path");
    Assert.Equal("effective", args[index + 1]);
    Assert.DoesNotContain("--b3-worker", args);
    Assert.DoesNotContain(args, argument => argument.StartsWith("O:", StringComparison.Ordinal));
  }

  [Fact]
  public void B3ChildHelperCannotMintASelectionForBoogieOptions() {
    var options = Options();
    options.Set(B3OptionBag.VerificationBackend, B3OptionBag.Backend.Boogie);
    Assert.Throws<InvalidOperationException>(() => ExecutableBackend.B3DafnyChildArguments(options, "program.doo", true));
  }

  private static DafnyOptions Options() {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(B3OptionBag.VerificationBackend, B3OptionBag.Backend.B3);
    return options;
  }

  private sealed class Loader : ITextDocumentLoader {
    internal IReadOnlyList<DafnyFile>? ParsedRoots;
    internal int ParseCalls;
    internal bool MissingResolution;
    internal bool MissingScope;
    public async Task<ProgramParseResult> ParseAsync(Compilation compilation, CancellationToken cancellationToken) {
      ParsedRoots = await compilation.RootFiles;
      ParseCalls++;
      var parsed = await ProgramParser.Parse("method M() { }", ParsedRoots[0].Uri,
        new BatchErrorReporter(compilation.Options));
      return new ProgramParseResult(parsed.Program, new Dictionary<Uri, int>());
    }
    public Task<ResolutionResult?> ResolveAsync(Compilation compilation, Microsoft.Dafny.Program program,
      CancellationToken cancellationToken) => Task.FromResult<ResolutionResult?>(
        MissingResolution ? null : new ResolutionResult(false, program, MissingScope ? null :
          new Dictionary<Uri, IntervalTree.IIntervalTree<DafnyPosition, ICanVerify>>()));
  }
  private sealed class Verifier : IProgramVerifier {
    internal int Calls;
    public Task<IReadOnlyList<IVerificationWorkItem>> GetVerificationTasksAsync(IVerificationBackend backend,
      ResolutionResult resolution, ModuleDefinition moduleDefinition, CancellationToken cancellationToken) {
      Calls++;
      throw new Exception("Root ownership does not permit verification work");
    }
  }
  private sealed class Backend : IVerificationBackend {
    public string Name => "b3";
    public VerificationCapabilities Capabilities => VerificationCapabilities.None;
    public Task<IReadOnlyList<IVerificationWorkItem>> PrepareAsync(VerificationPreparation input,
      CancellationToken cancellationToken) => throw new Exception("Root ownership does not permit backend work");
    public void Dispose() { }
  }
}
