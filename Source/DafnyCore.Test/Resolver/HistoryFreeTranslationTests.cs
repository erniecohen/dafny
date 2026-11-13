using System.Collections.Immutable;
using System.CommandLine;
using System.Reactive.Linq;
using Microsoft.Boogie.SMTLib;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Xunit;
using DafnyType = Microsoft.Dafny.Type;
using Program = Microsoft.Dafny.Program;

namespace DafnyCore.Test.Resolver;

[Collection("Cardinality resolution")]
public class HistoryFreeTranslationTests {
  [Fact]
  public void DefaultSolverDeclarationOrderUsesContentHashes() {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    Assert.True(options.DeterministicLiteralHashes);
    var literal = new Microsoft.Boogie.LiteralExpr(Microsoft.Boogie.Token.NoToken,
      Microsoft.BaseTypes.BigNum.FromString("18446744073709551616"));
    Assert.Equal(literal.GetContentHash(true), literal.GetContentHash(options.DeterministicLiteralHashes));
  }

  internal const string Units = """
    module Dependency {
      datatype D = A | B(x: int)
      function F(x: int): int { x + 1 }
      function Value(): int -> int { F }
    }
    module Client {
      import D = Dependency
      function G(x: int): int { var pair := (x, true); D.Value()(pair.0) }
      lemma L(x: int) ensures G(x) == x + 1 {}
      method InitArray<T>(f: int -> T) returns (a: array<T>)
        requires forall x :: 0 <= x < 12 ==> f.requires(x)
      { a := new T[12](f); }
      method PrintArray<T>(a: array<T>) {
        var i := 0;
        while i < a.Length {
          print a[i], " ";
          i := i + 1;
        }
      }
      method Work(x: int) returns (y: int) {
        var z := x;
        assert Before: z == x;
        y := z;
        assert y == x by { reveal Before; }
      }
    }
    """;

  [Fact]
  public void LookupSelectorsDoNotChangeResolutionInputs() {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    var expected = ModuleResolutionKey.Options(options).OrderBy(p => p.Key).ToArray();
    var translation = new Option<bool>("--translation-cache");
    var verification = new Option<string>("--verification-cache");
    foreach (var enabled in new[] { false, true }) {
      options.Set(translation, enabled);
      options.Set(verification, enabled ? "another-cache-directory" : "");
      Assert.Equal(expected, ModuleResolutionKey.Options(options).OrderBy(p => p.Key));
    }
    options.Set(CommonOptionBag.AdditionalAxioms, true);
    Assert.NotEqual(expected, ModuleResolutionKey.Options(options).OrderBy(p => p.Key).ToArray());
  }

  [Theory]
  [InlineData(false, 0)]
  [InlineData(true, 0)]
  [InlineData(false, 2)]
  [InlineData(true, 2)]
  public async Task ResizingAndMovingAnUnrelatedModuleCannotChangeOtherUnits(bool refresh, int threads) {
    var alone = await Resolve(Units, refresh, threads);
    var expected = Translate(alone, false);
    Assert.Equal(new[] { "Client", "Dependency" }, expected.Keys.Order());
    foreach (var size in new[] { 1, 64 }) {
      var unrelated = "module Unrelated {\n" + string.Join("\n", Enumerable.Range(0, size).Select(i =>
        $"function F{i}(x: bv77): (int, bool, int, int, int) {{ (0, true, 0, 0, 0) }}")) + "\n}\n";
      foreach (var source in new[] { unrelated + Units, Units + unrelated }) {
        var current = await Resolve(source, refresh, threads);
        var actual = Translate(current, true);
        foreach (var name in expected.Keys) {
          var module = current.RawModules().Single(m => m.Name == name);
          Assert.DoesNotContain(module.TranslationInputs!.ModuleNames, n => n.EndsWith(".Unrelated"));
          Assert.Equal(expected[name], actual[name]);
        }
      }
    }
    // The target really consumes its dependency: a dependency edit must change its text.
    var edited = await Resolve(Units.Replace("x + 1", "x + 2"), refresh, threads);
    Assert.NotEqual(expected["Client"], Translate(edited, false)["Client"]);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task RawSolverCommandOrderCannotDependOnEarlierTranslation(bool refresh) {
    // Synthetic responses test emission order only. This is not proof evidence.
    var before = await Resolve(Units, refresh, 0);
    var expected = await CaptureCommands(before);
    Assert.Contains(expected, command => command == "(check-sat)");
    var after = await Resolve("module Unrelated { " + string.Join("\n", Enumerable.Range(0, 64)
      .Select(i => $"function F{i}(x: bv77): bv77 {{ x + {i} }}")) + " }\n" + Units, refresh, 0);
    Translate(after, true);
    Assert.Equal(expected, await CaptureCommands(after));
  }

  internal static async Task<string[]> CaptureCommands(Program program, Microsoft.Boogie.Program? suppliedGraph = null) {
    var options = program.Options;
    options.VcsCores = 1;
    options.RestartProverPerVC = true;
    options.TimeLimit = 0;
    options.ProverOptions.Add("PROVER_PATH=synthetic-order-test");
    var commands = new List<string>();
    options.CreateSolver = (_, _) => new CommandCapture(commands);
    using var engine = new ExecutionEngine(options, new EmptyVerificationResultCache(), TaskScheduler.Default);
    var module = program.RawModules().Single(m => m.Name == "Client");
    var graph = suppliedGraph ?? new BoogieGenerator(program.Reporter, new ProofDependencyManager(), new(options)).DoTranslation(program, module);
    var tasks = await engine.GetVerificationTasks(graph, default);
    Assert.NotEmpty(tasks);
    foreach (var task in tasks) { await task.TryRun()!.LastAsync(); }
    return commands.ToArray();
  }

  private sealed class CommandCapture(List<string> commands) : SMTLibSolver {
    public override event Action<string> ErrorHandler { add { } remove { } }
    public override void Send(string command) => commands.Add(command);
    public override Task<SExpr> SendRequest(string request, CancellationToken cancellationToken = default) {
      commands.Add(request);
      return Task.FromResult(request == "(check-sat)" ? new SExpr("unsat") : new SExpr(":rlimit", new SExpr("0")));
    }
    public override Task PingPong() { commands.Add("<ping>"); return Task.CompletedTask; }
    public override void NewProblem(string name) => commands.Add("<problem>" + name);
    public override void AddErrorHandler(Action<string> handler) { }
    public override void Close() { }
    public override Task<IReadOnlyList<SExpr>> SendRequestsAndCloseInput(IReadOnlyList<string> requests,
      CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }

  private static Dictionary<string, string> Translate(Program program, bool reverse) {
    var modules = BoogieGenerator.VerifiableModules(program).Where(m => !m.IsDefaultModule).ToArray();
    if (reverse) { Array.Reverse(modules); }
    var result = new Dictionary<string, string>();
    foreach (var module in modules) {
      var manager = new ProofDependencyManager();
      var graph = new BoogieGenerator(program.Reporter, manager,
        new(program.Options) { InsertChecksums = true }).DoTranslation(program, module);
      Assert.NotEmpty(graph.TopLevelDeclarations);
      Assert.Equal(0, graph.Resolve(program.Options));
      Assert.Equal(0, graph.Typecheck(program.Options));
      using var text = new StringWriter();
      using (var writer = new TokenTextWriter(text, program.Options)) { graph.Emit(writer); }
      result.Add(module.Name, text.ToString());
    }
    return result;
  }

  private static async Task<Program> Resolve(string source, bool refresh, int threads) {
    DafnyType.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.ResolutionThreads, threads);
    options.Set(CommonOptionBag.TypeSystemRefresh, refresh);
    options.ModelViewFile = "-";
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(source, new Uri("untitled:history-free.dfy"), reporter);
    Assert.False(reporter.HasErrors, string.Join("\n", reporter.AllMessages.Select(d => d.Message)));
    await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    Assert.False(reporter.HasErrors, string.Join("\n", reporter.AllMessages.Select(d => d.Message)));
    return parsed.Program;
  }
}
