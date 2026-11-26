using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DafnyDriver.Commands;
using Microsoft.Dafny;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// The memory releases of issue 241, F4: a one-shot command-line run keeps no parsed clone and no
/// caches for a next edit (changes 1 and 2), releases the solver checkers of each reported module
/// (change 3), and, with --release-verified-modules, drops the bodies of a reported module
/// (change 4).  None changes what is reported.
/// </summary>
[Collection("Verification batch")]
public class MemoryReleaseTests {

  // Library is verified and reported before Client, whose translation reads the specifications of
  // Library's methods, constructor, lemma and iterator; Impl refines Spec.  Client has an error.
  private const string Program = @"
module Library {
  class Counter {
    var n: nat
    constructor (start: nat) ensures n == start { n := start; }
    method Bump() modifies this ensures n == old(n) + 1 { n := n + 1; }
  }
  method Twice(x: int) returns (y: int) ensures y == 2 * x { y := x + x; }
  lemma Square(x: int) ensures x * x >= 0 { }
  method Focused(x: int) returns (y: int) ensures y > x { y := x + 1; assert {:only} y > x; }
  iterator Countdown(start: nat) yields (k: nat) {
    var i := start;
    while i > 0 { yield i; i := i - 1; }
  }
}
abstract module Spec {
  method Choose(x: int) returns (y: int) ensures y > x
}
module Impl refines Spec {
  method Choose(x: int) returns (y: int) ensures y > x { y := x + 1; }
}
module Client {
  import L = Library
  import I = Impl
  method Use() returns (r: int) ensures r == 10 {
    var c := new L.Counter(3);
    c.Bump();
    assert c.n == 4;
    r := L.Twice(5);
    L.Square(r);
    var it := new L.Countdown(2);
    var more := it.MoveNext();
  }
  method Wrong() returns (r: int) ensures r > 0 {
    r := I.Choose(-5);
  }
}
";

  private record Run(string Output, List<string> Results, ExitValue Exit, CliCompilation Cli, Microsoft.Dafny.Program Program);

  /// <summary>
  /// Positive control for change 4, and its negative control: with the option, every body of a
  /// verified module is released, except one that holds an {:only} assertion, and the results,
  /// resource counts and output are those of a run without it, which releases none.
  /// </summary>
  [Theory]
  [InlineData(1)]
  [InlineData(4)]
  public async Task ReleasingVerifiedModulesChangesNoResult(int window) {
    using var directory = new TemporaryDirectory();
    var source = directory.Write("input.dfy", Program);
    var kept = await Verify(source, window, release: false);
    var released = await Verify(source, window, release: true);
    Assert.Equal(ExitValue.VERIFICATION_ERROR, kept.Exit);
    Assert.Equal(kept.Exit, released.Exit);
    SameResults(kept.Results, released.Results);
    Assert.Equal(kept.Output, released.Output);

    Assert.Equal(0, kept.Cli.Compilation.ReleasedBodies);
    var members = Members(kept.Program);
    Assert.All(members, m => Assert.False(m.BodyReleased));
    // Vacuity: the bodies the release would drop are there to drop.  Spec.Choose has none, nor
    // have the constructor and MoveNext an iterator declares, whose body is the iterator's own.
    Assert.All(members.Where(m => m.FullDafnyName != "Spec.Choose" && m.EnclosingClass is not IteratorDecl),
      m => Assert.NotNull(m.Body));

    var releasedMembers = Members(released.Program);
    var focused = releasedMembers.Single(m => m.FullDafnyName == "Library.Focused");
    Assert.False(focused.BodyReleased, "a body with an {:only} assertion is kept");
    Assert.NotNull(focused.Body);
    var dropped = releasedMembers.Where(m => m.BodyReleased).Select(m => m.FullDafnyName).ToHashSet();
    // Library (6 members, less Focused, plus the iterator), Spec, Impl and Client.
    Assert.Contains("Library.Counter._ctor", dropped);
    Assert.Contains("Library.Twice", dropped);
    Assert.Contains("Impl.Choose", dropped);
    Assert.Contains("Client.Use", dropped);
    Assert.Equal(dropped.Count + IteratorCount(released.Program), released.Cli.Compilation.ReleasedBodies);
  }

  /// <summary>The hook: a read of a released body throws, rather than see a body that is gone.</summary>
  [Fact]
  public async Task AReadOfAReleasedBodyThrows() {
    using var directory = new TemporaryDirectory();
    var source = directory.Write("input.dfy", Program);
    var released = await Verify(source, 1, release: true);
    var twice = Members(released.Program).Single(m => m.FullDafnyName == "Library.Twice");
    Assert.True(twice.BodyReleased);
    var read = Assert.Throws<InvalidOperationException>(() => twice.Body);
    Assert.Contains("--release-verified-modules", read.Message);
    Assert.Throws<InvalidOperationException>(() => twice.Children.ToList());
    // The body itself is dropped, not only hidden, so that its memory can be reused.
    Assert.Null(typeof(Method).GetField("body", System.Reflection.BindingFlags.NonPublic |
                                               System.Reflection.BindingFlags.Instance)!.GetValue(twice));
    Assert.NotEmpty(twice.ChildrenOtherThanBody);
    var iterator = released.Program.DefaultModuleDef.TopLevelDecls.OfType<LiteralModuleDecl>()
      .Single(m => m.Name == "Library").ModuleDef.TopLevelDecls.OfType<IteratorDecl>().Single();
    Assert.True(iterator.BodyReleased);
    Assert.Throws<InvalidOperationException>(() => iterator.Body);
  }

  /// <summary>
  /// Change 3: each reported module's checkers are released, which changes no result.  The solver
  /// input is the same byte for byte (the Boogie fork's CheckerReleaseTest); here, the resource counts.
  /// </summary>
  [Fact]
  public async Task CheckersOfReportedModulesAreReleased() {
    using var directory = new TemporaryDirectory();
    var source = directory.Write("input.dfy", Program);
    var run = await Verify(source, 1, release: false);
    Assert.True(run.Cli.Compilation.ReleasedCheckers > 0, "no checker was released; change 3 is untested");
    var again = await Verify(source, 1, release: false);
    SameResults(run.Results, again.Results);
  }

  /// <summary>
  /// Changes 1 and 2: a command-line compilation is one-shot, so the parsed program it hands on is
  /// not kept, and formatting, which reads it, is refused rather than see a program that is gone.
  /// </summary>
  [Fact]
  public async Task AOneShotCompilationKeepsNoParsedProgram() {
    using var directory = new TemporaryDirectory();
    var source = directory.Write("input.dfy", Program);
    var options = Options(source, 1, TextWriter.Null);
    var cli = CliCompilation.Create(options);
    using var compilation = cli.Compilation;
    Assert.True(compilation.Input.OneShot);
    cli.Start();
    Assert.NotNull(await cli.Resolution);
    Assert.Null(await compilation.ParsedProgram);
    await Assert.ThrowsAsync<InvalidOperationException>(() => compilation.GetTextEditToFormatCode(new Uri(source)));
  }

  // Modules in a window, and the proofs of one symbol, finish in any order.
  /// <summary>
  /// Neither loop keeps the verification tasks of a symbol once it is reported: a window's slots
  /// outlive their module, and must drop its states.  The run is still going when this is checked.
  /// </summary>
  [Theory]
  [InlineData(1)]
  [InlineData(4)]
  public async Task AReportedSymbolKeepsNoTasks(int window) {
    using var directory = new TemporaryDirectory();
    var source = directory.Write("input.dfy", Program);
    var options = Options(source, window, TextWriter.Null);
    var cli = CliCompilation.Create(options);
    using var compilation = cli.Compilation;
    cli.Start();
    Assert.NotNull(await cli.Resolution);
    var tasks = new List<WeakReference>();
    var lastModule = "Client";
    await foreach (var result in cli.VerifyAllLazily()) {
      if (result.CanVerify.FullDafnyName.StartsWith(lastModule + ".")) {
        // Everything reported before the last module, which is still being verified.
        Assert.True(await AllCollected(tasks), $"window {window}: a reported symbol's tasks are still reachable");
        break;
      }
      tasks.AddRange(WeakTasks(result));
    }
    Assert.NotEmpty(tasks);
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  private static List<WeakReference> WeakTasks(CanVerifyResult result) =>
    result.Results.Select(r => new WeakReference(r.Task)).ToList();

  private static async Task<bool> AllCollected(List<WeakReference> references) {
    for (var i = 0; i < 20 && references.Exists(r => r.IsAlive); i++) {
      GC.Collect();
      GC.WaitForPendingFinalizers();
      await Task.Delay(50);
    }
    return !references.Exists(r => r.IsAlive);
  }

  private static void SameResults(List<string> expected, List<string> actual) {
    var missing = expected.Except(actual).ToList();
    var extra = actual.Except(expected).ToList();
    Assert.True(missing.Count == 0 && extra.Count == 0 && expected.Count == actual.Count,
      $"expected only: {string.Join("; ", missing)}\nactual only: {string.Join("; ", extra)}");
  }

  private static List<MethodOrConstructor> Members(Microsoft.Dafny.Program program) =>
    program.DefaultModuleDef.TopLevelDecls.OfType<LiteralModuleDecl>()
      .SelectMany(m => m.ModuleDef.TopLevelDecls).OfType<TopLevelDeclWithMembers>()
      .SelectMany(d => d.Members).OfType<MethodOrConstructor>().ToList();

  private static int IteratorCount(Microsoft.Dafny.Program program) =>
    program.DefaultModuleDef.TopLevelDecls.OfType<LiteralModuleDecl>()
      .SelectMany(m => m.ModuleDef.TopLevelDecls).OfType<IteratorDecl>().Count();

  private static async Task<Run> Verify(string source, int window, bool release) {
    var output = new StringWriter();
    var options = Options(source, window, output);
    options.Set(VerifyCommand.ReleaseVerifiedModules, release);
    var cli = CliCompilation.Create(options);
    var compilation = cli.Compilation;
    cli.Start();
    var resolution = await cli.Resolution;
    Assert.NotNull(resolution);
    var results = new List<string>();
    using (var verificationResults = new Subject<CanVerifyResult>()) {
      VerifyCommand.ReportVerificationDiagnostics(cli, verificationResults);
      var summarized = VerifyCommand.ReportVerificationSummary(cli, verificationResults);
      await foreach (var result in cli.VerifyAllLazily()) {
        results.Add(result.CanVerify.FullDafnyName + ": " + string.Join(",",
          result.Results.Select(r => r.Result.Outcome + "/" + r.Result.ResourceCount).Order()));
        verificationResults.OnNext(result);
      }
      verificationResults.OnCompleted();
      await summarized;
    }
    var exit = await cli.GetAndReportExitValue();
    compilation.Dispose();
    return new Run(output.ToString(), results, exit, cli, resolution.ResolvedProgram);
  }

  private static DafnyOptions Options(string source, int window, TextWriter output) {
    var options = new DafnyOptions(TextReader.Null, output, output);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.UnicodeCharacters, true);
    options.Set(VerifyCommand.VerificationModuleWindow, (uint)window);
    options.VcsCores = 4;
    // Resource counts are compared only with a fresh solver per proof (see ModuleWindowTests).
    options.RestartProverPerVC = true;
    options.DeterministicLiteralHashes = true;
    options.ResourceLimit = 16000000;
    options.TimeLimit = 0;
    if (Environment.GetEnvironmentVariable("Z3") is { } solver) {
      options.ProverOptions.Add("PROVER_PATH=" + solver);
    }
    options.CliRootSourceUris.Add(new Uri(source));
    return options;
  }

  private sealed class TemporaryDirectory : IDisposable {
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dafny-memory-release-" + Guid.NewGuid());

    public TemporaryDirectory() {
      Directory.CreateDirectory(Path);
    }

    public string Write(string name, string text) {
      var file = System.IO.Path.Combine(Path, name);
      File.WriteAllText(file, text);
      return file;
    }

    public void Dispose() {
      Directory.Delete(Path, true);
    }
  }
}
