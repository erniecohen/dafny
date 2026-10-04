using System;
using System.IO;
using Microsoft.Dafny;
using Xunit;

namespace DafnyCore.Test;

public class B3BackendSelectionTest {
  [Fact]
  public void SelectingB3DoesNotInvokeTheBoogieEngineFactory() {
    var options = new DafnyOptions(DafnyOptions.Default);
    options.Set(B3OptionBag.VerificationBackend, B3OptionBag.Backend.B3);
    using var backend = VerificationBackendFactory.Create(options, () => throw new Exception("Boogie was constructed"));
    Assert.IsType<B3VerificationBackend>(backend);
    Assert.Equal("b3", backend.Name);
  }
  [Fact]
  public void B3SolverPreparationDoesNotExecuteTheSolverInTheParent() {
    var path = Path.GetTempFileName();
    try {
      // This is deliberately not an executable: a parent Process.Start would fail.
      var options = new DafnyOptions(DafnyOptions.Default);
      options.Set(B3OptionBag.VerificationBackend, B3OptionBag.Backend.B3);
      options.TimeLimit = 20;
      options.ProverOptions.Add("PROVER_PATH=" + path);
      var reporter = new BatchErrorReporter(options);
      options.ProcessSolverOptions(reporter, Token.Cli);
      // Missing worker is a reported error; the empty file was never executed.
      Assert.True(reporter.HasErrors);
      Assert.Contains(reporter.AllMessages, message => message.Message.Contains("worker package"));
      Assert.Null(options.SolverVersion);
    } finally { File.Delete(path); }
  }
  [Fact]
  public void DefaultSelectionInvokesTheBoogieFactory() {
    var options = new DafnyOptions(DafnyOptions.Default);
    Assert.Throws<InvalidOperationException>(() => VerificationBackendFactory.Create(options,
      () => throw new InvalidOperationException("selected Boogie")));
  }
}
