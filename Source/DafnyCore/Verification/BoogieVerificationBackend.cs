#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Boogie;
using VC;
using VCGeneration;

namespace Microsoft.Dafny;

public record AssertionBatchResult(Implementation Implementation, VerificationRunResult Result);

/// <summary>Preserves the existing Boogie task execution, cache, splitting and result objects.</summary>
public sealed class BoogieVerificationBackend : IVerificationBackend {
  public ExecutionEngine Engine { get; }
  public string Name => "boogie";
  public VerificationCapabilities Capabilities => VerificationCapabilities.SeededVerification |
    VerificationCapabilities.Counterexamples | VerificationCapabilities.ProofDependencies |
    VerificationCapabilities.ResourceCounts | VerificationCapabilities.AssertionIsolation;

  public BoogieVerificationBackend(ExecutionEngine engine) { Engine = engine; }

  public async Task<IReadOnlyList<IVerificationWorkItem>> PrepareAsync(VerificationPreparation input,
    CancellationToken cancellationToken) {
    var tasks = await Engine.GetVerificationTasks(input.IntermediateProgram, cancellationToken);
    return tasks.Select(task => (IVerificationWorkItem)new BoogieVerificationWorkItem(task)).ToList();
  }

  public void Dispose() { Engine.Dispose(); }
}

public sealed class BoogieVerificationWorkItem : IVerificationWorkItem {
  public IVerificationTask Task { get; }
  public VerificationIdentity Identity { get; }
  public VerificationSourceInfo Source { get; }

  public BoogieVerificationWorkItem(IVerificationTask task) {
    Task = task;
    var key = task.ScopeId + task.Split.SplitIndex;
    if (task.ScopeToken is RefinementOrigin refinement) {
      key += "." + refinement.InheritingModule.Name;
    }
    Identity = new VerificationIdentity(task.ScopeId, key, task.Split.SplitIndex, task.Split.RandomSeed);
    var origin = BoogieGenerator.ToDafnyToken(task.Token);
    var scopeOrigin = BoogieGenerator.ToDafnyToken(task.ScopeToken);
    var owner = ((CanVerifyOrigin)task.ScopeToken).CanVerify;
    var hidden = task.Split.HiddenFunctions.Select(f => f.tok).OfType<FromDafnyNode>()
      .Select(n => n.Node).OfType<Function>().Distinct().OrderBy(f => f.Origin.Center).ToList();
    var name = task.Split.Implementation.Name;
    var kind = name.StartsWith("CheckWellformed") ? VerificationUnitKind.Wellformedness :
      name.StartsWith("OverrideCheck") ? VerificationUnitKind.Override : VerificationUnitKind.Body;
    Source = new VerificationSourceInfo(owner, scopeOrigin, origin, kind,
      task.Split.Implementation.VerboseName, DescribePart(task.Split.Token,
        name.Contains("CheckWellFormed$"), true), hidden);
  }

  public VerificationStatus CacheStatus => ConvertStatus(Task.CacheStatus);
  public bool IsIdle => Task.IsIdle;
  public IVerificationWorkItem FromSeed(int newSeed) => new BoogieVerificationWorkItem(Task.FromSeed(newSeed));
  public IObservable<VerificationStatus>? TryRun() => Task.TryRun()?.Select(ConvertStatus);
  public void Cancel() => Task.Cancel();

  private static VerificationStatus ConvertStatus(IVerificationStatus status) => status switch {
    Stale => new VerificationStale(), Queued => new VerificationQueued(), Running => new VerificationRunning(),
    Completed completed => new VerificationCompleted(ConvertResult(completed.Result)),
    _ => throw new ArgumentOutOfRangeException(nameof(status))
  };

  private static VerificationResult ConvertResult(VerificationRunResult result) {
    Dictionary<AssertCmd, SolverOutcome> outcomes;
    Dictionary<AssertCmd, Counterexample> counterexamples;
    if (result.Outcome == SolverOutcome.Valid) {
      outcomes = result.Asserts.Distinct().ToDictionary(assertion => assertion, _ => SolverOutcome.Valid);
      counterexamples = new();
    } else {
      result.ComputePerAssertOutcomes(out outcomes, out counterexamples);
    }
    var assertions = result.Asserts.Select(assertion => {
      counterexamples.TryGetValue(assertion, out var counterexample);
      var secondary = counterexample switch {
        ReturnCounterexample returned => BoogieGenerator.ToDafnyToken(returned.FailingReturn.tok),
        CallCounterexample called => BoogieGenerator.ToDafnyToken(called.FailingRequires.tok),
        _ => null
      };
      return new VerificationAssertion(assertion.UniqueId.ToString(),
        BoogieGenerator.ToDafnyToken(assertion.tok), secondary, assertion.Description?.SuccessDescription ?? "assertion",
        ConvertOutcome(outcomes[assertion]));
    }).ToList();
    return new VerificationResult(ConvertOutcome(result.Outcome), Array.Empty<DafnyDiagnostic>(), assertions,
      true, result.StartTime, result.RunTime, result.ResourceCount, result.CounterExamples.Count,
      result.MaxCounterExamples == result.CounterExamples.Count, result);
  }

  public static VerificationOutcome ConvertOutcome(SolverOutcome outcome) => outcome switch {
    SolverOutcome.Valid => VerificationOutcome.Verified, SolverOutcome.Invalid => VerificationOutcome.Failed,
    SolverOutcome.Undetermined => VerificationOutcome.Unknown, SolverOutcome.TimeOut => VerificationOutcome.TimedOut,
    SolverOutcome.OutOfResource => VerificationOutcome.OutOfResource, SolverOutcome.OutOfMemory => VerificationOutcome.OutOfMemory,
    SolverOutcome.Bounded => VerificationOutcome.Bounded, _ => VerificationOutcome.ToolError
  };

  private static string DescribePart(IImplementationPartOrigin origin, bool wellformedness, bool outer) {
    if (outer && origin is ImplementationRootOrigin) {
      return wellformedness ? "contract consistency" : "entire body";
    }
    var result = origin switch {
      PathOrigin path => DescribePart(path.Inner, wellformedness, false) + "after executing lines " + string.Join(", ", path.BranchTokens.Select(b => b.line)),
      RemainingAssertionsOrigin remaining => DescribePart(remaining.Origin, wellformedness, false) + (outer ? "remaining assertions" : ""),
      IsolatedAssertionOrigin isolated => DescribePart(isolated.Origin, wellformedness, false) + $"assertion at line {isolated.line}",
      JumpOrigin jump => DescribePart(jump.Origin, wellformedness, false) + $"{(jump.IsolatedReturn is GotoCmd ? "continue" : "return")} at line {jump.line}",
      AfterSplitOrigin split => DescribePart(split.Inner, wellformedness, false) + $"assertions after split_here at line {split.line}",
      FocusOrigin focus => DescribePart(focus.Inner, wellformedness, false) + "with focus " + string.Join(", ", focus.FocusChoices.Select(b => (b.DidFocus ? "+" : "-") + b.Token.line)),
      UntilFirstSplitOrigin until => DescribePart(until.Inner, wellformedness, false) + "assertions until first split",
      ImplementationRootOrigin => "", _ => throw new ArgumentOutOfRangeException(nameof(origin))
    };
    return !outer && !string.IsNullOrEmpty(result) ? result + ", " : result;
  }
}
