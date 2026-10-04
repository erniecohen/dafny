using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using DafnyB3Host;
using DafnyB3Protocol;
using Xunit;
using NormalizedProgram = DafnyB3Protocol.Program;

namespace DafnyB3Host.Test;

public sealed class HostIntegrationTests {
  private static NormalizedProgram Unit(Statement body, params Binding[] variables) => new(
    Array.Empty<string>(), Array.Empty<Function>(), Array.Empty<Axiom>(), new Unit("sUnit", variables, body));
  private static SolverConfiguration._IConfiguration NativeConfiguration() =>
    SolverConfiguration.Configuration.create(RawAstBuilder.S(SolverPath),
      Dafny.Sequence<Dafny.ISequence<Dafny.Rune>>.FromArray(new[] { RawAstBuilder.S("-in"), RawAstBuilder.S("-smt2") }),
      new BigInteger(10000), new BigInteger(1000000), new BigInteger(1048576),
      SolverConfiguration.SolverKind.create_Z3(), new BigInteger(2));
  private static string SolverPath => Environment.GetEnvironmentVariable("B3_TEST_SOLVER")
    ?? throw new InvalidOperationException("Set B3_TEST_SOLVER to the pinned solver executable");
  private static Request Request(WorkerPackage package, NormalizedProgram program, params string[] obligations) => new(
    Protocol.Version, Guid.NewGuid().ToString("N"), Protocol.NormalizerVersion, WorkerPackage.UpstreamCommit,
    Protocol.GetProgramHash(program), program.Unit.Name, program,
    new Configuration(SolverPath, new[] { "-in", "-smt2" }, 10000, 1000000, 1048576, 2, "5.1.0", Digest(SolverPath)),
    obligations.Select(id => new SourceIdentity(id, "program.dfy", 1, 1, "assertion")).ToArray(), package.Fingerprint);
  private static string Digest(string file) {
    using var input = File.OpenRead(file);
    return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
  }
  private static Task<Completion> Run(WorkerPackage package, Request request) =>
    new WorkerProcessClient("dotnet", new[] { package.WorkerPath }).RunAsync(request, CancellationToken.None);

  [Theory]
  [InlineData(true, Outcome.Verified)]
  [InlineData(false, Outcome.Failed)]
  public async Task NormalizedTrueAndFalseHaveDistinctVerdicts(bool condition, Outcome expected) {
    using var package = new PackageFixture();
    var request = Request(package.Package, Unit(new Check("sO0", new BooleanLiteral(condition), false)), "sO0");
    var result = await Run(package.Package, request);
    Assert.True(result.TraversalCompleted, result.Error);
    Assert.Null(result.Error);
    Assert.Equal(expected, result.Outcome);
    Assert.Equal(request.WorkerFingerprint, result.WorkerFingerprint);
    Assert.Equal(request.UnitId, result.UnitId);
    var attempt = Assert.Single(result.Attempts);
    Assert.Equal(0, attempt.Sequence);
    Assert.Equal("sO0", attempt.ObligationId);
    Assert.Equal(expected, attempt.Outcome);
    Assert.Contains("sO0", attempt.Description);
    Assert.NotNull(attempt.Breadcrumbs);
  }

  [Fact]
  public async Task ExplicitIntegerStateIsConstructedAndChecked() {
    using var package = new PackageFixture();
    var body = new Block(new Statement[] {
      new Assign("sV0", new IntegerLiteral("42")),
      new Check("sO0", new Operation(Operator.Equal, "bool", new Expression[] {
        new Variable("sV0", "int"), new IntegerLiteral("42") }), false)
    });
    var request = Request(package.Package, Unit(body, new Binding("sV0", "int")), "sO0");
    var result = await Run(package.Package, request);
    Assert.True(result.TraversalCompleted, result.Error);
    Assert.Null(result.Error);
    Assert.Equal(Outcome.Verified, result.Outcome);
    Assert.Equal(Outcome.Verified, Assert.Single(result.Attempts).Outcome);
  }

  [Fact]
  public async Task BranchAttemptsCarrySourceIdentityAndBreadcrumbs() {
    using var package = new PackageFixture();
    var program = Unit(new Conditional(new Variable("sV0", "bool"),
      new Check("sO0", new BooleanLiteral(true), false),
      new Check("sO1", new BooleanLiteral(false), false)), new Binding("sV0", "bool"));
    var result = await Run(package.Package, Request(package.Package, program, "sO0", "sO1"));
    Assert.True(result.TraversalCompleted, result.Error);
    Assert.Null(result.Error);
    Assert.Equal(Outcome.Failed, result.Outcome);
    Assert.Equal(2, result.Attempts.Count);
    Assert.Equal(new[] { "sO0", "sO1" }, result.Attempts.Select(attempt => attempt.ObligationId));
    Assert.All(result.Attempts, attempt => Assert.NotEmpty(attempt.Breadcrumbs!));
  }

  [Fact]
  public async Task AFailedLearningAssertionCannotHideTheUnitFailure() {
    using var package = new PackageFixture();
    var program = Unit(new Block(new Statement[] {
      new Check("sO0", new BooleanLiteral(false), true),
      new Check("sO1", new BooleanLiteral(false), false)
    }));
    var result = await Run(package.Package, Request(package.Package, program, "sO0", "sO1"));
    Assert.True(result.TraversalCompleted, result.Error);
    Assert.Null(result.Error);
    Assert.Equal(Outcome.Failed, result.Outcome);
    Assert.Equal(new[] { 0, 1 }, result.Attempts.Select(attempt => attempt.Sequence));
    Assert.Equal(new[] { "sO0", "sO1" }, result.Attempts.Select(attempt => attempt.ObligationId));
    Assert.Equal(Outcome.Failed, result.Attempts[0].Outcome);
  }

  [Fact]
  public void IllTypedRawInputIsRejectedByTheLibrary() {
    var raw = RawAstBuilder.Build(Unit(new Check("sO0", new IntegerLiteral("1"), false)));
    var result = B3Library.__default.CheckAndVerify(raw, RawAstBuilder.S("sUnit"), NativeConfiguration());
    Assert.False(result.dtor_complete);
    Assert.True(result.dtor_error.is_Some);
    Assert.StartsWith("invalid input:", result.dtor_error.dtor_value.ToVerbatimString(false));
    Assert.Empty(result.dtor_attempts);
  }

  [Fact]
  public void UnsupportedRawSignatureTypesAreRejectedByTheLibrary() {
    var raw = RawAstBuilder.Build(Unit(new Check("sO0", new BooleanLiteral(true), false)));
    raw = RawAst.Program.create_Program(Dafny.Set<Dafny.ISequence<Dafny.Rune>>.FromElements(RawAstBuilder.S("sT0")),
      raw.dtor_domains, raw.dtor_types, raw.dtor_taggers, raw.dtor_functions, raw.dtor_axioms, raw.dtor_procedures);
    var result = B3Library.__default.CheckAndVerify(raw, RawAstBuilder.S("sUnit"), NativeConfiguration());
    Assert.False(result.dtor_complete);
    Assert.True(result.dtor_error.is_Some);
    Assert.StartsWith("unsupported:", result.dtor_error.dtor_value.ToVerbatimString(false));
    Assert.Empty(result.dtor_attempts);
  }

  [Fact]
  public void MissingSelectedUnitCannotComplete() {
    var raw = RawAstBuilder.Build(Unit(new Check("sO0", new BooleanLiteral(true), false)));
    var result = B3Library.__default.CheckAndVerify(raw, RawAstBuilder.S("sOther"), NativeConfiguration());
    Assert.False(result.dtor_complete);
    Assert.True(result.dtor_error.is_Some);
    Assert.Empty(result.dtor_attempts);
  }

  [Fact]
  public async Task WrongSolverDigestCannotVerify() {
    using var package = new PackageFixture();
    var request = Request(package.Package, Unit(new Check("sO0", new BooleanLiteral(true), false)), "sO0");
    request = request with { Configuration = request.Configuration with { SolverSha256 = new string('b', 64) } };
    var result = await Run(package.Package, request);
    Assert.False(result.TraversalCompleted);
    Assert.Equal(Outcome.ToolError, result.Outcome);
    Assert.Contains("digest mismatch", result.Error);
    Assert.Empty(result.Attempts);
  }

  [Theory]
  [InlineData(0L)]
  [InlineData(SolverFileIdentity.MaximumExecutableBytes + 1)]
  public async Task EmptyAndOversizedSolverFilesCannotVerify(long length) {
    using var package = new PackageFixture();
    var executable = Path.Combine(package.Directory, "invalid-solver");
    using (var file = File.OpenWrite(executable)) { file.SetLength(length); }
    var request = Request(package.Package, Unit(new Check("sO0", new BooleanLiteral(true), false)), "sO0");
    request = request with { Configuration = request.Configuration with {
      SolverExecutable = executable, SolverSha256 = new string('b', 64) } };
    var result = await Run(package.Package, request);
    Assert.False(result.TraversalCompleted);
    Assert.Equal(Outcome.ToolError, result.Outcome);
    Assert.Contains("exceeds file bounds", result.Error);
    Assert.Empty(result.Attempts);
  }

  [Fact]
  public async Task WrongActualSolverVersionCannotVerify() {
    using var package = new PackageFixture();
    var executable = ProbeFixture(package.Directory, "wrong-version");
    var request = Request(package.Package, Unit(new Check("sO0", new BooleanLiteral(true), false)), "sO0");
    request = request with { Configuration = request.Configuration with {
      SolverExecutable = executable, SolverSha256 = Digest(executable) } };
    var result = await Run(package.Package, request);
    Assert.False(result.TraversalCompleted);
    Assert.Equal(Outcome.ToolError, result.Outcome);
    Assert.Contains("version mismatch", result.Error);
    Assert.Empty(result.Attempts);
  }

  [Theory]
  [InlineData("oversized")]
  [InlineData("nonzero")]
  [InlineData("empty")]
  public async Task InvalidVersionProbeOutputIsRejected(string scenario) {
    using var package = new PackageFixture();
    var executable = ProbeFixture(package.Directory, scenario);
    var configuration = Request(package.Package, Unit(new Check("sO0", new BooleanLiteral(true), false)), "sO0").Configuration
      with { SolverExecutable = executable, SolverSha256 = Digest(executable) };
    await Assert.ThrowsAsync<InvalidDataException>(() => SolverIdentity.ValidateAsync(configuration));
  }

  [Fact]
  public async Task SilentVersionProbeExceedsItsDeadline() {
    using var package = new PackageFixture();
    var executable = ProbeFixture(package.Directory, "silent");
    var configuration = Request(package.Package, Unit(new Check("sO0", new BooleanLiteral(true), false)), "sO0").Configuration
      with { SolverExecutable = executable, SolverSha256 = Digest(executable), TimeoutMilliseconds = 1000 };
    await Assert.ThrowsAsync<TimeoutException>(() => SolverIdentity.ValidateAsync(configuration));
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public async Task DirectIdentityChecksRequirePositiveDeadlines(int timeout) {
    using var package = new PackageFixture();
    var configuration = Request(package.Package, Unit(new Check("sO0", new BooleanLiteral(true), false)), "sO0").Configuration
      with { TimeoutMilliseconds = timeout };
    await Assert.ThrowsAsync<InvalidDataException>(() => SolverIdentity.ValidateAsync(configuration));
  }

  [Fact]
  public async Task CallerCancellationIsDistinctFromProbeDeadline() {
    using var package = new PackageFixture();
    var executable = ProbeFixture(package.Directory, "silent");
    var configuration = Request(package.Package, Unit(new Check("sO0", new BooleanLiteral(true), false)), "sO0").Configuration
      with { SolverExecutable = executable, SolverSha256 = Digest(executable) };
    using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SolverIdentity.ValidateAsync(configuration, cancellation.Token));
  }

  [Fact]
  public async Task NonExecutableFilesCannotVerify() {
    using var package = new PackageFixture();
    var executable = Path.Combine(package.Directory, "not-executable");
    File.WriteAllText(executable, "This is not an executable.");
    var request = Request(package.Package, Unit(new Check("sO0", new BooleanLiteral(true), false)), "sO0");
    request = request with { Configuration = request.Configuration with {
      SolverExecutable = executable, SolverSha256 = Digest(executable) } };
    var result = await Run(package.Package, request);
    Assert.False(result.TraversalCompleted);
    Assert.Equal(Outcome.ToolError, result.Outcome);
    Assert.Empty(result.Attempts);
  }

  [Fact]
  public async Task MissingExecutableCannotVerify() {
    using var package = new PackageFixture();
    var request = Request(package.Package, Unit(new Check("sO0", new BooleanLiteral(true), false)), "sO0");
    request = request with { Configuration = request.Configuration with {
      SolverExecutable = Path.Combine(package.Directory, "missing-executable") } };
    var result = await Run(package.Package, request);
    Assert.False(result.TraversalCompleted);
    Assert.Equal(Outcome.ToolError, result.Outcome);
    Assert.Empty(result.Attempts);
  }

  [Fact]
  public async Task VersionProbeDrainsBoundedStderr() {
    using var package = new PackageFixture();
    var executable = ProbeFixture(package.Directory, "stderr");
    var configuration = Request(package.Package, Unit(new Check("sO0", new BooleanLiteral(true), false)), "sO0").Configuration
      with { SolverExecutable = executable, SolverSha256 = Digest(executable) };
    await SolverIdentity.ValidateAsync(configuration);
  }

  private static string ProbeFixture(string directory, string scenario) {
    if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) {
      throw new PlatformNotSupportedException("B3 worker fixtures require Unix");
    }
    var executable = Path.Combine(directory, "probe-solver");
    File.WriteAllText(executable, "#!/usr/bin/env python3\n" +
      "import sys,time\n" +
      "assert sys.argv[1:] == ['-version']\n" +
      "scenario = '" + scenario + "'\n" +
      "if scenario == 'silent': time.sleep(10)\n" +
      "elif scenario == 'wrong-version': print('Z3 version 4.16.0 - 64 bit')\n" +
      "elif scenario == 'oversized': print('x' * 20000)\n" +
      "elif scenario == 'nonzero': print('Z3 version 5.1.0 - 64 bit'); sys.exit(7)\n" +
      "elif scenario == 'stderr': sys.stderr.write('x' * 100000); print('Z3 version 5.1.0 - 64 bit')\n",
      new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) {
      File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
    return executable;
  }

  [Fact]
  public async Task WrongWorkerFingerprintCannotVerify() {
    using var package = new PackageFixture();
    var request = Request(package.Package, Unit(new Check("sO0", new BooleanLiteral(true), false)), "sO0")
      with { WorkerFingerprint = new string('b', 64) };
    var result = await Run(package.Package, request);
    Assert.False(result.TraversalCompleted);
    Assert.Equal(Outcome.ToolError, result.Outcome);
    Assert.Contains("different worker build", result.Error);
  }

  [Fact]
  public async Task HostRejectsAChangedPackageBeforeAnySolverWork() {
    using var package = new PackageFixture();
    var request = Request(package.Package, Unit(new Check("sO0", new BooleanLiteral(true), false)), "sO0");
    File.AppendAllText(Path.Combine(package.Directory, "DafnyB3Host.deps.json"), "\n");
    Assert.Throws<InvalidDataException>(() => WorkerPackage.Load(package.Package.WorkerPath));
    var result = await Run(package.Package, request);
    Assert.False(result.TraversalCompleted);
    Assert.Equal(Outcome.ToolError, result.Outcome);
    Assert.Contains("package digest mismatch", result.Error);
  }

  private sealed class PackageFixture : IDisposable {
    public string Directory { get; } = Path.Combine(Path.GetTempPath(), "b3-host-" + Guid.NewGuid().ToString("N"));
    public WorkerPackage Package { get; }
    public PackageFixture() {
      var source = Environment.GetEnvironmentVariable("B3_TEST_WORKER_PACKAGE")
        ?? throw new InvalidOperationException("Set B3_TEST_WORKER_PACKAGE to the published worker package");
      System.IO.Directory.CreateDirectory(Directory);
      foreach (var file in System.IO.Directory.GetFiles(source)) { File.Copy(file, Path.Combine(Directory, Path.GetFileName(file))); }
      Package = WorkerPackage.Load(Path.Combine(Directory, "DafnyB3Host.dll"));
    }
    public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
  }
}
