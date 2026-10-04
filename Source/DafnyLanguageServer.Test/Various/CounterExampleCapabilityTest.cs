using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.Handlers.Custom;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Extensions;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Util;
using Microsoft.Dafny.LanguageServer.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.JsonRpc.Server;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Dafny.LanguageServer.IntegrationTest.Various;

public class CounterExampleCapabilityTest : ClientBasedLanguageServerTest {
  public CounterExampleCapabilityTest(ITestOutputHelper output) : base(output) {
  }

  private DafnyCounterExampleHandler CreateHandler(TelemetryPublisherBase telemetryPublisher = null) => new(
    Server.GetRequiredService<DafnyOptions>(),
    Server.GetRequiredService<ILogger<DafnyCounterExampleHandler>>(),
    Projects, telemetryPublisher ?? Server.GetRequiredService<TelemetryPublisherBase>());

  [Fact]
  public async Task B3CounterexamplesAreRejectedBeforeVerification() {
    await SetUp(options => {
      options.Set(B3OptionBag.VerificationBackend, B3OptionBag.Backend.B3);
      options.Set(ProjectManager.Verification, VerifyOnMode.Never);
    });
    var document = CreateTestDocument("method Foo() { assert false; }", "B3Counterexamples.dfy");
    await client.OpenDocumentAndWaitAsync(document, CancellationToken);
    var request = new CounterExampleParams {
      TextDocument = new TextDocumentIdentifier { Uri = document.Uri }
    };
    var telemetryPublisher = new Mock<TelemetryPublisherBase>(Mock.Of<ILogger<TelemetryPublisherBase>>());
    var exception = await Assert.ThrowsAsync<RpcErrorException>(() =>
      CreateHandler(telemetryPublisher.Object).Handle(request, CancellationToken));
    Assert.Equal(ErrorCodes.InvalidRequest, exception.Code);
    Assert.Equal("The b3 verification backend does not provide counterexample models.", exception.Message);

    // Exercise the custom request on the wire as well as its detailed server error.
    var responseException = await Assert.ThrowsAnyAsync<RequestException>(() => client.SendRequest(request, CancellationToken));
    Assert.Equal(ErrorCodes.InvalidRequest, responseException.ErrorCode);
    var manager = await Projects.GetProjectManager(request.TextDocument);
    Assert.NotNull(manager);
    var state = await manager.States.FirstAsync().ToTask(CancellationToken);
    Assert.NotEmpty(state.CanVerifyStates[document.Uri.ToUri()]);
    Assert.All(state.CanVerifyStates[document.Uri.ToUri()].Values,
      verifiable => Assert.Equal(VerificationPreparationState.NotStarted, verifiable.PreparationProgress));
    telemetryPublisher.Verify(publisher => publisher.PublishTelemetry(It.IsAny<ImmutableDictionary<string, object>>()),
      Times.Never());
  }

  [Fact]
  public async Task CancellingCounterexampleRequestStopsWaitingForUnfinishedVerification() {
    await SetUp(options => options.Set(ProjectManager.Verification, VerifyOnMode.Never));
    var document = CreateTestDocument("method {:neverVerify} Foo() { assert false; }", "CancelledCounterexamples.dfy");
    await client.OpenDocumentAndWaitAsync(document, CancellationToken);
    var request = new CounterExampleParams {
      TextDocument = new TextDocumentIdentifier { Uri = document.Uri }
    };
    var manager = await Projects.GetProjectManager(request.TextDocument);
    Assert.NotNull(manager);
    await manager.VerifyEverythingAsync(document.Uri.ToUri());
    await manager.States.Where(state => state.CanVerifyStates.TryGetValue(document.Uri.ToUri(), out var verifiables) &&
                                       verifiables.Values.Any(verifiable => verifiable.PreparationProgress == VerificationPreparationState.Done &&
                                                                           verifiable.VerificationTasks.Count != 0)).
      FirstAsync().ToTask(CancellationToken);

    using var cancellation = new CancellationTokenSource();
    var response = CreateHandler().Handle(request, cancellation.Token);
    Assert.False(response.IsCompleted);
    cancellation.Cancel();
    Assert.Empty(await response.WaitAsync(CancellationToken));
  }
}
