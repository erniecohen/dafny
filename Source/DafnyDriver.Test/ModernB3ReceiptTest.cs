using DafnyDriver.Commands;
using Microsoft.Dafny;
using static DafnyDriver.Test.ModernB3TestSupport;

namespace DafnyDriver.Test;

[Collection("Modern B3 CLI")]
public class ModernB3ReceiptTest {
  [Fact]
  public async Task CompletedInventoryAuthorizesOnlyItsOriginalProgram() {
    var program = await Resolve();
    var owner = Owner(program);
    var task = new WorkItem(owner);
    var ledger = new CliVerificationLedger(program, new[] { owner.ContainingModule }, new[] { owner });
    ledger.BeginPreparation(owner.ContainingModule);
    Assert.True(ledger.CompletePreparation(owner.ContainingModule, new[] { task }));
    ledger.CompleteUnit(owner.ContainingModule, task, Result());
    ledger.ReleaseModule(owner.ContainingModule);
    var receipt = ledger.Seal(true, CancellationToken.None);
    Assert.Equal(CliVerificationDisposition.CompleteNonempty, receipt.Disposition);
    Assert.Equal(1, receipt.UnitCount);
    Assert.Equal(64, receipt.InventoryHash.Length);
    var calls = 0;
    Assert.Equal((true, true), await ModernCliCompilation.ContinueAsync(receipt, program, CancellationToken.None,
      () => { calls++; return Task.FromResult(true); }));
    var different = await Resolve(program.Options);
    Assert.Equal((false, false), await ModernCliCompilation.ContinueAsync(receipt, different, CancellationToken.None,
      () => { calls++; return Task.FromResult(true); }));
    Assert.Equal(1, calls);
  }

  [Fact]
  public async Task EmptyScopeRequiresSuccessfulExplicitModulePreparationAndRelease() {
    var program = await Resolve();
    var module = Owner(program).ContainingModule;
    var ledger = new CliVerificationLedger(program, new[] { module }, Array.Empty<ICanVerify>());
    ledger.BeginPreparation(module);
    Assert.True(ledger.CompletePreparation(module, Array.Empty<IVerificationWorkItem>()));
    ledger.ReleaseModule(module);
    var receipt = ledger.Seal(true, CancellationToken.None);
    Assert.Equal(CliVerificationDisposition.CompleteEmpty, receipt.Disposition);
    Assert.True(receipt.Authorizes(program, CancellationToken.None));
    Assert.Equal(0, receipt.UnitCount);
    Assert.Contains("no eligible checking units", receipt.Reason);
  }

  [Theory]
  [InlineData("not-started")]
  [InlineData("preparing")]
  [InlineData("failed")]
  [InlineData("missing-result")]
  [InlineData("not-released")]
  [InlineData("duplicate-module")]
  [InlineData("duplicate-unit")]
  [InlineData("unknown-owner")]
  [InlineData("missing-owner-module")]
  [InlineData("mutated-identity")]
  [InlineData("different-task")]
  [InlineData("repeated-result")]
  [InlineData("fatal-diagnostics")]
  [InlineData("cancelled")]
  [InlineData("changed-selection")]
  [InlineData("empty-key")]
  [InlineData("empty-scope-id")]
  [InlineData("changed-owner-after-result")]
  public async Task IncompleteOrChangedInventoryNeverCallsCompiler(string defect) {
    var program = await Resolve();
    var owner = Owner(program);
    var module = owner.ContainingModule;
    var task = new WorkItem(owner);
    if (defect == "empty-key") { task.Identity = task.Identity with { Key = "" }; }
    if (defect == "empty-scope-id") { task.Identity = task.Identity with { ScopeId = "" }; }
    var ledger = new CliVerificationLedger(program,
      defect == "missing-owner-module" ? Array.Empty<ModuleDefinition>() :
      defect == "duplicate-module" ? new[] { module, module } : new[] { module }, new[] { owner });
    if (defect != "not-started") { ledger.BeginPreparation(module); }
    if (defect == "failed") { ledger.FailedPreparation(module, "translation failed before parts"); }
    else if (defect != "not-started" && defect != "preparing") {
      var prepared = defect == "duplicate-unit" ? new[] { task, task } :
        defect == "unknown-owner" ? new[] { new WorkItem(Owner(program, "N")) } : new[] { task };
      ledger.CompletePreparation(module, prepared);
      if (defect == "mutated-identity") { task.Identity = task.Identity with { Key = "different" }; }
      if (defect != "missing-result") {
        ledger.CompleteUnit(module, defect == "different-task" ? new WorkItem(owner) : task, Result());
      }
      if (defect == "repeated-result") { ledger.CompleteUnit(module, task, Result()); }
      if (defect == "changed-owner-after-result") { task.Source = task.Source with { CanVerify = Owner(program, "N") }; }
      if (defect != "not-released") { ledger.ReleaseModule(module); }
    }
    using var cancellation = new CancellationTokenSource();
    if (defect == "cancelled") { cancellation.Cancel(); }
    if (defect == "changed-selection") { program.Options.ResourceLimit++; }
    var receipt = ledger.Seal(defect != "fatal-diagnostics", cancellation.Token);
    Assert.Equal(CliVerificationDisposition.Rejected, receipt.Disposition);
    var calls = 0;
    Assert.Equal((false, false), await ModernCliCompilation.ContinueAsync(receipt, program, cancellation.Token,
      () => { calls++; return Task.FromResult(true); }));
    Assert.Equal(0, calls);
  }

  [Theory]
  [InlineData(VerificationOutcome.Failed)]
  [InlineData(VerificationOutcome.Unknown)]
  [InlineData(VerificationOutcome.TimedOut)]
  [InlineData(VerificationOutcome.OutOfResource)]
  [InlineData(VerificationOutcome.OutOfMemory)]
  [InlineData(VerificationOutcome.Cancelled)]
  [InlineData(VerificationOutcome.Unsupported)]
  [InlineData(VerificationOutcome.ToolError)]
  [InlineData(VerificationOutcome.Bounded)]
  public async Task EveryNonVerifiedOutcomeRejectsContinuation(VerificationOutcome outcome) {
    var program = await Resolve();
    var owner = Owner(program);
    var task = new WorkItem(owner);
    var ledger = new CliVerificationLedger(program, new[] { owner.ContainingModule }, new[] { owner });
    ledger.BeginPreparation(owner.ContainingModule);
    ledger.CompletePreparation(owner.ContainingModule, new[] { task });
    ledger.CompleteUnit(owner.ContainingModule, task, Result(outcome));
    ledger.ReleaseModule(owner.ContainingModule);
    var receipt = ledger.Seal(true, CancellationToken.None);
    Assert.Equal(CliVerificationDisposition.Rejected, receipt.Disposition);
    Assert.Contains(outcome.ToString(), receipt.Reason);
  }

  [Theory]
  [InlineData("backend")]
  [InlineData("worker")]
  [InlineData("solver")]
  [InlineData("limit")]
  [InlineData("prover-options")]
  public async Task ConfigurationChangedAfterSealingCannotAuthorizeOutput(string field) {
    var program = await Resolve();
    var module = Owner(program).ContainingModule;
    var ledger = new CliVerificationLedger(program, new[] { module }, Array.Empty<ICanVerify>());
    ledger.BeginPreparation(module);
    ledger.CompletePreparation(module, Array.Empty<IVerificationWorkItem>());
    ledger.ReleaseModule(module);
    var receipt = ledger.Seal(true, CancellationToken.None);
    switch (field) {
      case "backend": program.Options.Set(B3OptionBag.VerificationBackend, B3OptionBag.Backend.Boogie); break;
      case "worker": program.Options.Set(B3OptionBag.Worker, new FileInfo("changed-worker.dll")); break;
      case "solver": program.Options.Set(BoogieOptionBag.SolverPath, new FileInfo("changed-solver")); break;
      case "limit": program.Options.TimeLimit++; break;
      case "prover-options": program.Options.ProverOptions.Add("changed"); break;
    }
    Assert.False(receipt.Authorizes(program, CancellationToken.None));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task OrdinarySkipIsDistinctFromHiddenOrUnrecordedSkip(bool ordinary) {
    var options = Options();
    options.Verify = false;
    if (ordinary) { Skip(options); }
    var program = await Resolve(options);
    var receipt = CliVerificationLedger.Disabled(program);
    Assert.Equal(ordinary ? CliVerificationDisposition.Disabled : CliVerificationDisposition.Rejected,
      receipt.Disposition);
    Assert.False(receipt.VerificationAttempted);
    Assert.Equal(ordinary, receipt.Authorizes(program, CancellationToken.None));
    if (ordinary) {
      options.Set(BoogieOptionBag.HiddenNoVerify, true);
      Assert.Equal(CliVerificationDisposition.Rejected, CliVerificationLedger.Disabled(program).Disposition);
    }
  }
}
