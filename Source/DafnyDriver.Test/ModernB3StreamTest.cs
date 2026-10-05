using System.Reactive.Disposables;
using System.Reactive.Linq;
using DafnyDriver.Commands;
using Microsoft.Dafny;
using static DafnyDriver.Test.ModernB3TestSupport;

namespace DafnyDriver.Test;

[Collection("Modern B3 CLI")]
public class ModernB3StreamTest {
  [Theory]
  [InlineData("missing")]
  [InlineData("duplicate")]
  [InlineData("after-terminal")]
  [InlineData("terminal-fault")]
  [InlineData("no-stream")]
  [InlineData("cancelled-after-terminal")]
  public async Task ATerminalValueWithoutNormalStreamCompletionCannotVerify(string defect) {
    var owner = Owner(await Resolve());
    var disposed = false;
    using var cancellation = new CancellationTokenSource();
    var task = new WorkItem(owner, run: () => defect == "no-stream" ? null :
      Observable.Create<VerificationStatus>(observer => {
        if (defect != "missing") { observer.OnNext(new VerificationCompleted(Result())); }
        if (defect == "duplicate") { observer.OnNext(new VerificationCompleted(Result())); }
        if (defect == "after-terminal") { observer.OnNext(new VerificationRunning()); }
        if (defect == "cancelled-after-terminal") { cancellation.Cancel(); }
        if (defect == "terminal-fault") { observer.OnError(new InvalidDataException("worker fault")); }
        else { observer.OnCompleted(); }
        return Disposable.Create(() => disposed = true);
      }));
    await Assert.ThrowsAnyAsync<Exception>(() => ModernCliVerification.RunUnitAsync(task, cancellation.Token));
    Assert.Equal(1, task.RunCalls);
    Assert.Equal(defect != "no-stream", disposed);
  }

  [Fact]
  public async Task NormalStreamCompletionRetainsTheExactVerifiedResult() {
    var owner = Owner(await Resolve());
    var expected = Result();
    var task = new WorkItem(owner, run: () => Observable.Return<VerificationStatus>(new VerificationCompleted(expected)));
    Assert.Same(expected, await ModernCliVerification.RunUnitAsync(task, CancellationToken.None));
  }
}
