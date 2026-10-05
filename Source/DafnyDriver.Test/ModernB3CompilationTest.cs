using System.Reactive.Linq;
using System.Reactive.Subjects;
using DafnyDriver.Commands;
using Microsoft.Dafny;
using static DafnyDriver.Test.ModernB3TestSupport;

namespace DafnyDriver.Test;

[Collection("Modern B3 CLI")]
public class ModernB3CompilationTest {
  [Theory]
  [InlineData("build", "verified")]
  [InlineData("run", "verified")]
  [InlineData("test", "verified")]
  [InlineData("build", "failed")]
  [InlineData("run", "failed")]
  [InlineData("test", "failed")]
  [InlineData("build", "unsupported")]
  [InlineData("run", "unsupported")]
  [InlineData("test", "unsupported")]
  [InlineData("build", "skip")]
  [InlineData("run", "skip")]
  [InlineData("test", "skip")]
  public async Task ModernCommandsGateTargetHooksOnTheSelectedBackend(string command, string result) {
    using var source = new SourceFile(command == "test" ? "method {:test} Positive() { assert true; }" :
      "method Main() { assert true; }");
    var options = Options();
    options.CliRootSourceUris.Add(new Uri(source.Path));
    options.RunAfterCompile = command != "build";
    if (command == "test") {
      options.Set(RunAllTestsMainMethod.IncludeTestRunner, true);
      options.MainMethod = RunAllTestsMainMethod.SyntheticTestMainName;
    }
    if (result == "skip") {
      Skip(options);
      options.Set(B3OptionBag.Worker, new FileInfo(Path.Combine(source.Directory, "missing-worker.dll")));
      options.Set(BoogieOptionBag.SolverPath, new FileInfo(Path.Combine(source.Directory, "missing-solver")));
    }
    var backend = new Backend {
      MakeUnit = (owner, key) => new WorkItem(owner, key, () => Observable.Return<VerificationStatus>(
        new VerificationCompleted(Result(result == "failed" ? VerificationOutcome.Failed :
          result == "unsupported" ? VerificationOutcome.Unsupported : VerificationOutcome.Verified))))
    };
    var compilerCalls = 0;
    var exit = await ModernCliCompilation.RunB3Async(options, CancellationToken.None,
      Services(backend, program => {
        compilerCalls++;
        Assert.Equal(1, backend.DisposeCalls);
        Assert.Equal(command != "build", program.Options.RunAfterCompile);
        if (result != "skip") {
          Assert.NotEmpty(backend.Programs);
          Assert.All(backend.Programs, verifiedProgram => Assert.Same(verifiedProgram, program));
          Assert.All(backend.Units, unit => Assert.Equal(1, unit.RunCalls));
        }
        return Task.FromResult(true);
      }));
    var allowed = result is "verified" or "skip";
    Assert.Equal(allowed ? 0 : (int)ExitValue.VERIFICATION_ERROR, exit);
    Assert.Equal(allowed ? 1 : 0, compilerCalls);
    Assert.Equal(1, backend.DisposeCalls);
    if (result == "skip") { Assert.Equal(0, backend.PrepareCalls); Assert.Empty(backend.Units); }
  }

  [Fact]
  public async Task VerificationAndCompilerShareTheResolvedAstAfterSourceFileDisappears() {
    using var source = new SourceFile();
    var options = Options();
    options.CliRootSourceUris.Add(new Uri(source.Path));
    var backend = new Backend { BeforePrepare = _ => File.Delete(source.Path) };
    var compilerCalls = 0;
    var exit = await ModernCliCompilation.RunB3Async(options, CancellationToken.None,
      Services(backend, program => {
        compilerCalls++;
        Assert.Same(Assert.Single(backend.Programs), program);
        Assert.False(File.Exists(source.Path));
        Assert.Equal("Main", OwnerFrom(program).Name);
        return Task.FromResult(true);
      }));
    Assert.Equal(0, exit);
    Assert.Equal(1, compilerCalls);
  }

  [Fact]
  public async Task TerminalThenFaultRejectsCompilerAndFinishesOwnedCleanup() {
    using var source = new SourceFile();
    var options = Options();
    options.CliRootSourceUris.Add(new Uri(source.Path));
    var disposed = 0;
    var backend = new Backend {
      MakeUnit = (owner, key) => new WorkItem(owner, key, () => TerminalThenFault(() => disposed++))
    };
    var compilerCalls = 0;
    var exit = await ModernCliCompilation.RunB3Async(options, CancellationToken.None,
      Services(backend, _ => { compilerCalls++; return Task.FromResult(true); })).WaitAsync(TimeSpan.FromSeconds(10));
    Assert.Equal((int)ExitValue.VERIFICATION_ERROR, exit);
    Assert.Equal(0, compilerCalls);
    Assert.Equal(1, disposed);
    Assert.Equal(1, backend.DisposeCalls);
    Assert.All(backend.Units, unit => Assert.True(unit.CancelCalls > 0));
  }

  [Fact]
  public async Task EveryPreparedConsumerReceivesTheOriginalErrorAfterAResult() {
    using var source = new SourceFile();
    var options = Options();
    options.CliRootSourceUris.Add(new Uri(source.Path));
    var (_, inputs) = await PreparedCliInputs.PrepareAsync(options, CancellationToken.None);
    Assert.NotNull(inputs);
    using var compilation = CliCompilation.CreatePreparedB3(options, inputs!, () => new Backend());
    compilation.Compilation.ShouldProcessSolverOptions = false;
    compilation.Start();
    var resolution = await compilation.Resolution;
    Assert.NotNull(resolution);
    var owner = OwnerFrom(resolution!.ResolvedProgram);
    var task = new WorkItem(owner);
    using var reported = new Subject<CanVerifyResult>();
    var consumers = VerifyCommand.RegisterPreparedConsumers(compilation, resolution, reported);
    Assert.Equal(3, consumers.Count);
    Assert.All(consumers, consumer => Assert.False(consumer.IsCompleted));
    reported.OnNext(new CanVerifyResult(owner, new[] { new VerificationWorkItemResult(task, Result()) }));
    var failure = new InvalidDataException("failure after published owner result");
    reported.OnError(failure); // Must not throw before the remaining consumers receive this error.
    await Assert.ThrowsAsync<InvalidDataException>(() => Task.WhenAll(consumers).WaitAsync(TimeSpan.FromSeconds(10)));
    Assert.All(consumers, consumer => {
      Assert.True(consumer.IsFaulted);
      Assert.Same(failure, Assert.Single(consumer.Exception!.InnerExceptions));
    });
  }

  [Theory]
  [InlineData("translation")]
  [InlineData("parse")]
  [InlineData("resolution")]
  [InlineData("cancelled")]
  public async Task FailedPreparationNeverReachesTargetHooks(string defect) {
    using var source = new SourceFile(defect == "parse" ? "method {" :
      defect == "resolution" ? "method Main() { assert unknown; }" : "method Main() { assert true; }");
    var options = Options();
    options.CliRootSourceUris.Add(new Uri(source.Path));
    var backend = new Backend {
      PreparationFailure = defect == "translation" ? new InvalidDataException("failed before parts") : null
    };
    using var cancellation = new CancellationTokenSource();
    if (defect == "cancelled") { cancellation.Cancel(); }
    var compilerCalls = 0;
    var exit = await ModernCliCompilation.RunB3Async(options, cancellation.Token,
      Services(backend, _ => { compilerCalls++; return Task.FromResult(true); }));
    Assert.NotEqual(0, exit);
    Assert.Equal(0, compilerCalls);
    if (defect == "translation") { Assert.Equal(1, backend.PrepareCalls); }
    else { Assert.Equal(0, backend.PrepareCalls); }
  }

  [Fact]
  public async Task EmptyPreparedScopeReportsNoAssertionProved() {
    using var source = new SourceFile("module Empty { }");
    var output = new StringWriter();
    var options = Options(output);
    options.CliRootSourceUris.Add(new Uri(source.Path));
    var backend = new Backend { Empty = true };
    var calls = 0;
    var exit = await ModernCliCompilation.RunB3Async(options, CancellationToken.None,
      Services(backend, _ => { calls++; return Task.FromResult(true); }));
    Assert.Equal(0, exit);
    Assert.Equal(1, calls);
    Assert.Empty(backend.Units);
    Assert.Contains("no assertion was proved", output.ToString());
  }

  [Fact]
  public async Task CompilerFailureRemainsNonzeroAfterSuccessfulSelectedVerification() {
    using var source = new SourceFile();
    var options = Options();
    options.CliRootSourceUris.Add(new Uri(source.Path));
    var backend = new Backend();
    var calls = 0;
    var exit = await ModernCliCompilation.RunB3Async(options, CancellationToken.None,
      Services(backend, _ => { calls++; return Task.FromResult(false); }));
    Assert.Equal((int)ExitValue.COMPILE_ERROR, exit);
    Assert.Equal(1, calls);
    Assert.Equal(1, backend.DisposeCalls);
  }

  [Fact]
  public async Task CancellationDuringTargetContinuationCannotReturnAcceptedSuccess() {
    using var source = new SourceFile();
    var options = Options();
    options.CliRootSourceUris.Add(new Uri(source.Path));
    Skip(options);
    using var cancellation = new CancellationTokenSource();
    var backend = new Backend();
    var calls = 0;
    var exit = await ModernCliCompilation.RunB3Async(options, cancellation.Token,
      Services(backend, _ => { calls++; cancellation.Cancel(); return Task.FromResult(true); }));
    Assert.Equal((int)ExitValue.VERIFICATION_ERROR, exit);
    Assert.Equal(1, calls);
    Assert.Equal(1, backend.DisposeCalls);
    Assert.Equal(0, backend.PrepareCalls);
    // The existing target API has no new process cancellation contract; this only gates the returned disposition.
  }

  [Fact]
  public void ExplicitB3SelectionNeverInvokesTheBoogieFactory() {
    var calls = 0;
    using var selected = VerificationBackendFactory.Create(Options(), () => {
      calls++;
      throw new InvalidOperationException("Boogie factory must remain unreachable");
    });
    Assert.Equal("b3", selected.Name);
    Assert.Equal(0, calls);
  }

  private static Method OwnerFrom(Microsoft.Dafny.Program program) =>
    SymbolExtensions.GetSymbolDescendants(program.DefaultModule).OfType<Method>().Single(method => method.Name == "Main");
}
