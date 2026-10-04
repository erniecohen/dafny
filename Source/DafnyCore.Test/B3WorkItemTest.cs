using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny;
using Xunit;

namespace DafnyCore.Test;

public class B3WorkItemTest {
  private static B3WorkItem Item(Func<CancellationToken, Task<VerificationResult>> run) => new(
    new VerificationIdentity("unit", "b3:unit", 0, 0),
    new VerificationSourceInfo(null!, Token.Cli, Token.Cli, VerificationUnitKind.Other,
      "unit", "unit", Array.Empty<Function>()), run);
  private static VerificationResult Success() => new(VerificationOutcome.Verified,
    Array.Empty<DafnyDiagnostic>(), Array.Empty<VerificationAssertion>(), true,
    DateTime.UtcNow, TimeSpan.Zero, null, 0);

  [Fact]
  public void CompletingAnOldSubscriptionCannotCancelTheNewRun() {
    var tokens = new List<CancellationToken>();
    var secondResult = new TaskCompletionSource<VerificationResult>();
    var item = Item(token => {
      tokens.Add(token);
      return tokens.Count == 1 ? Task.FromResult(Success()) : secondResult.Task;
    });
    IDisposable? secondSubscription = null;
    using var first = item.TryRun()!.Subscribe(status => {
      if (status is VerificationCompleted) { secondSubscription = item.TryRun()!.Subscribe(); }
    });
    Assert.Equal(2, tokens.Count);
    Assert.False(tokens[1].IsCancellationRequested);
    Assert.IsType<VerificationRunning>(item.CacheStatus);
    secondSubscription!.Dispose();
    Assert.True(tokens[1].IsCancellationRequested);
    secondResult.SetResult(Success());
  }

  [Fact]
  public async Task CancellationBeforeSubscriptionCannotStartTheWorker() {
    var ran = false;
    var item = Item(_ => { ran = true; return Task.FromResult(Success()); });
    var observable = item.TryRun()!;
    item.Cancel();
    var result = Assert.IsType<VerificationCompleted>(await observable.LastAsync().ToTask());
    Assert.False(ran);
    Assert.Equal(VerificationOutcome.Cancelled, result.Result.Outcome);
    Assert.True(item.IsIdle);
  }

  [Fact]
  public async Task AWorkerExceptionLeavesTheTaskRestartable() {
    var item = Item(_ => throw new InvalidOperationException("worker failure"));
    await Assert.ThrowsAsync<InvalidOperationException>(async () => await item.TryRun()!.LastAsync().ToTask());
    Assert.True(item.IsIdle);
    Assert.IsType<VerificationStale>(item.CacheStatus);
  }
}
