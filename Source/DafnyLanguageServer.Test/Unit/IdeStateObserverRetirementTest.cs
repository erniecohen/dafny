using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.Workspace;
using Microsoft.Dafny.LanguageServer.Workspace.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.Dafny.LanguageServer.IntegrationTest.Unit;

public class IdeStateObserverRetirementTest {
  [Fact]
  public void RetiredObserverCannotPublishLateCompletionOrClear() {
    var state = InitialState();
    var publisher = new RecordingPublisher();
    var observer = Observer(state, publisher);
    observer.OnNext(state with { Input = state.Input with { Version = 1 } });
    observer.Retire();
    observer.OnNext(state with { Input = state.Input with { Version = 100 }, Status = CompilationStatus.ResolutionSucceeded });
    observer.Clear();
    observer.Retire();
    Assert.Equal(new[] { 1, 2 }, publisher.Versions);
  }

  [Fact]
  public async Task RetirementSerializesWithAnInFlightPublication() {
    var state = InitialState();
    using var release = new ManualResetEventSlim();
    var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var retirementStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var publisher = new RecordingPublisher(snapshot => {
      if (snapshot.Version == 1) {
        entered.TrySetResult(true);
        if (!release.Wait(TimeSpan.FromSeconds(10))) { throw new TimeoutException("Publication barrier was not released"); }
      }
    });
    var observer = Observer(state, publisher);
    var publication = Task.Run(() => observer.OnNext(state with { Input = state.Input with { Version = 1 } }));
    Task retirement = Task.CompletedTask;
    try {
      await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
      retirement = Task.Run(() => { retirementStarted.TrySetResult(true); observer.Retire(); });
      await retirementStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
    } finally {
      release.Set();
      await Task.WhenAll(publication, retirement);
    }
    // A replacement can publish only after the old publication and its final clear have finished.
    Observer(state, publisher).OnNext(state with { Input = state.Input with { Version = 3 } });
    observer.OnNext(state with { Input = state.Input with { Version = 100 } });
    Assert.Equal(new[] { 1, 2, 3 }, publisher.Versions);
  }

  private static IdeState InitialState() {
    var options = DafnyOptions.Default;
    var uri = new Uri("file:///observer-retirement/dfyconfig.toml");
    var project = new DafnyProject(null, uri, null, new HashSet<string>(), new HashSet<string>(),
      new Dictionary<string, object>());
    return IdeState.InitialIdeState(new CompilationInput(options, 0, project));
  }
  private static IdeStateObserver Observer(IdeState state, INotificationPublisher publisher) =>
    new(NullLogger<IdeStateObserver>.Instance, new NoTelemetryPublisher(), publisher, state);
  private sealed class NoTelemetryPublisher() : TelemetryPublisherBase(NullLogger<TelemetryPublisherBase>.Instance) {
    public override void PublishTelemetry(ImmutableDictionary<string, object> data) { }
  }
  private sealed class RecordingPublisher(Action<IdeState> publish = null) : INotificationPublisher {
    public List<int> Versions { get; } = new();
    public void PublishNotifications(IdeState previousState, IdeState state) {
      publish?.Invoke(state);
      Versions.Add(state.Version);
    }
  }
}
