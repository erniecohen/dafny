using Microsoft.Boogie;

namespace DafnyCore.Test.Verification;

// Run explicitly with the pinned native solver. Ordinary structural build
// probes select Obligation tests and never execute this class.
public class NativeBoogieContractSemanticsTests {
  [Theory]
  [InlineData("free-ensures-no-second-proof", "procedure P(); free ensures false; implementation P() { return; }", true)]
  [InlineData("free-ensures-available-at-call", "procedure P() returns (x:int); free ensures x == 42; procedure C(); implementation C() { var x:int; call x := P(); assert x == 42; }", true)]
  [InlineData("free-requires-no-call-check", "procedure P(); free requires false; procedure C(); implementation C() { call P(); }", true)]
  [InlineData("free-requires-callee-assumption", "procedure P(x:int); free requires x > 0; implementation P(x:int) { assert x > 0; }", true)]
  [InlineData("checked-local-negative", "procedure P(); free ensures false; implementation P() { assert false; return; }", false)]
  [InlineData("checked-call-negative", "procedure P(); free requires false; procedure C(); implementation C() { assert false; call P(); }", false)]
  [InlineData("checked-boilerplate-retained", "procedure P(x:int); free requires true; requires x > 0; procedure C(); implementation C() { call P(0); }", false)]
  public async Task FreeContractsPreserveAssumptionsWithoutDuplicateChecks(string name, string source, bool expected) {
    var solver = Environment.GetEnvironmentVariable("DAFNY_SOLVER_PATH")
      ?? Environment.GetEnvironmentVariable("DAFNY_SOLVER")
      ?? throw new InvalidOperationException("Native contract test requires an explicitly pinned solver");
    var directory = Path.Combine(Path.GetTempPath(), "boogie-contract-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try {
      var file = Path.Combine(directory, name + ".bpl");
      await File.WriteAllTextAsync(file, source);
      using var output = new StringWriter();
      var options = new CommandLineOptions(output, new ConsolePrinter()) {
        RunningBoogieFromCommandLine = true
      };
      Assert.True(options.Parse(new[] { file, "/proverOpt:PROVER_PATH=" + solver, "/vcsCores:1", "/timeLimit:30" }));
      using var engine = ExecutionEngine.CreateWithoutSharedCache(options);
      var result = await engine.ProcessFiles(output, new List<string> { file });
      Assert.DoesNotContain("resolution error", output.ToString());
      Assert.DoesNotContain("type checking error", output.ToString());
      Assert.DoesNotContain("timed out", output.ToString());
      Assert.Equal(expected, result);
    } finally {
      Directory.Delete(directory, true);
    }
  }
}
