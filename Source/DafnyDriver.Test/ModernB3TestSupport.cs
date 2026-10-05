using System.Reactive.Disposables;
using System.Reactive.Linq;
using DafnyDriver.Commands;
using Microsoft.Dafny;
using DafnyProgram = Microsoft.Dafny.Program;

namespace DafnyDriver.Test;

[CollectionDefinition("Modern B3 CLI", DisableParallelization = true)]
public sealed class ModernB3CliCollection { }

internal static class ModernB3TestSupport {
  internal static DafnyOptions Options(StringWriter? output = null, TextReader? input = null) {
    var options = new DafnyOptions(input ?? TextReader.Null, output ?? new StringWriter(), new StringWriter());
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(B3OptionBag.VerificationBackend, B3OptionBag.Backend.B3);
    options.Set(CommonOptionBag.UnicodeCharacters, true);
    options.Set(CommonOptionBag.AllowWarnings, true);
    options.ApplyBinding(CommonOptionBag.AllowWarnings);
    options.CompilerName = "py";
    options.UsingNewCli = true;
    options.Verify = true;
    options.TimeLimit = 20;
    return options;
  }

  internal static void Skip(DafnyOptions options) {
    options.Set(BoogieOptionBag.NoVerify, true);
    options.ApplyBinding(BoogieOptionBag.NoVerify);
  }

  internal static VerificationResult Result(VerificationOutcome outcome = VerificationOutcome.Verified,
    bool complete = true) => new(outcome, Array.Empty<DafnyDiagnostic>(),
      Array.Empty<VerificationAssertion>(), complete, DateTime.UnixEpoch, TimeSpan.Zero, null, 0);

  internal static async Task<DafnyProgram> Resolve(DafnyOptions? options = null) {
    Microsoft.Dafny.Type.ResetScopes();
    var reporter = new BatchErrorReporter(options ?? Options());
    var parsed = await ProgramParser.Parse("method M() { assert true; } method N() { }",
      new Uri("untitled:modern-b3-receipt.dfy"), reporter);
    await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    Assert.Equal(0, reporter.ErrorCount);
    return parsed.Program;
  }

  internal static Method Owner(DafnyProgram program, string name = "M") =>
    SymbolExtensions.GetSymbolDescendants(program.DefaultModule).OfType<Method>().Single(method => method.Name == name);

  internal sealed class WorkItem : IVerificationWorkItem {
    internal WorkItem(ICanVerify owner, string key = "unit", Func<IObservable<VerificationStatus>?>? run = null) {
      Identity = new VerificationIdentity(owner.FullDafnyName, key, 0, 0);
      Source = new VerificationSourceInfo(owner, owner.Origin, owner.Origin, VerificationUnitKind.Body,
        owner.FullDafnyName, "stub checking unit", Array.Empty<Function>());
      Run = run ?? (() => Observable.Return<VerificationStatus>(new VerificationCompleted(Result())));
    }
    public VerificationIdentity Identity { get; set; }
    public VerificationSourceInfo Source { get; set; }
    internal Func<IObservable<VerificationStatus>?> Run { get; set; }
    internal int CancelCalls { get; private set; }
    internal int RunCalls { get; private set; }
    public VerificationStatus CacheStatus => new VerificationStale();
    public IVerificationWorkItem FromSeed(int newSeed) => this;
    public IObservable<VerificationStatus>? TryRun() { RunCalls++; return Run(); }
    public bool IsIdle => true;
    public void Cancel() { CancelCalls++; }
  }

  internal sealed class Backend : IVerificationBackend {
    public string Name => "b3";
    public VerificationCapabilities Capabilities => VerificationCapabilities.None;
    internal int PrepareCalls { get; private set; }
    internal int DisposeCalls { get; private set; }
    internal List<WorkItem> Units { get; } = new();
    internal List<DafnyProgram> Programs { get; } = new();
    internal Action<VerificationPreparation>? BeforePrepare { get; set; }
    internal Func<ICanVerify, string, WorkItem>? MakeUnit { get; set; }
    internal bool Empty { get; set; }
    internal Exception? PreparationFailure { get; set; }
    public Task<IReadOnlyList<IVerificationWorkItem>> PrepareAsync(VerificationPreparation input,
      CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      PrepareCalls++;
      Programs.Add(input.Resolution.ResolvedProgram);
      BeforePrepare?.Invoke(input);
      if (PreparationFailure != null) { throw PreparationFailure; }
      // This is an orchestration stub, not evidence that any source assertion was proved.
      var tasks = Empty ? Array.Empty<WorkItem>() : input.Resolution.CanVerifies!.Values
        .SelectMany(tree => tree.Values).Distinct<ICanVerify>(ReferenceEqualityComparer.Instance)
        .Where(owner => ReferenceEquals(owner.ContainingModule, input.Module))
        .Select((owner, index) => MakeUnit?.Invoke(owner, "stub-" + index) ?? new WorkItem(owner, "stub-" + index)).ToArray();
      Units.AddRange(tasks);
      return Task.FromResult<IReadOnlyList<IVerificationWorkItem>>(tasks);
    }
    public void Dispose() { DisposeCalls++; }
  }

  internal sealed class SourceFile : IDisposable {
    internal string Directory { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    internal string Path { get; }
    internal SourceFile(string source = "method Main() { assert true; }") {
      System.IO.Directory.CreateDirectory(Directory);
      Path = System.IO.Path.Combine(Directory, "program.dfy");
      File.WriteAllText(Path, source);
    }
    public void Dispose() { System.IO.Directory.Delete(Directory, true); }
  }

  internal static ModernCliServices Services(Backend backend,
    Func<DafnyProgram, Task<bool>> compile) => new((options, inputs) => {
      var compilation = CliCompilation.CreatePreparedB3(options, inputs, () => backend);
      // The backend is an in-process orchestration stub. Its controls do not request a worker or solver.
      compilation.Compilation.ShouldProcessSolverOptions = false;
      return compilation;
    }, (program, _, _, _) => compile(program));

  internal static IObservable<VerificationStatus> TerminalThenFault(Action disposed) =>
    Observable.Create<VerificationStatus>(observer => {
      observer.OnNext(new VerificationCompleted(Result()));
      observer.OnError(new InvalidDataException("terminal followed by source failure"));
      return Disposable.Create(disposed);
    });
}
