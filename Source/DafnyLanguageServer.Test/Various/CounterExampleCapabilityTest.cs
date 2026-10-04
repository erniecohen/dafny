using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DafnyB3Protocol;
using Microsoft.Dafny.LanguageServer.Handlers.Custom;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Extensions;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Util;
using Microsoft.Dafny.LanguageServer.Workspace;
using Microsoft.Dafny.LanguageServer.Workspace.Notifications;
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
    using var package = new PreflightOnlyWorkerPackage(GetFreshTempPath());
    await SetUp(options => {
      options.Set(B3OptionBag.VerificationBackend, B3OptionBag.Backend.B3);
      options.Set(B3OptionBag.Worker, new FileInfo(package.Worker));
      options.Set(BoogieOptionBag.VerificationTimeLimit, 30u);
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
    // Fail explicitly if package/configuration preflight prevented real unit discovery,
    // rather than waiting forever for an impossible successful-resolution state.
    var resolution = await manager.Compilation.Resolution;
    Assert.NotNull(resolution);
    Assert.False(resolution.HasErrors);
    Assert.NotNull(resolution.CanVerifies);
    var uri = document.Uri.ToUri();
    // The replayed state can still precede resolution. Require actual source units
    // before checking that capability rejection left their verification unstarted.
    var state = await manager.States.Where(candidate => candidate.Status == CompilationStatus.ResolutionSucceeded &&
                                                       candidate.CanVerifyStates.TryGetValue(uri, out var sourceUnits) && sourceUnits.Count != 0).
      FirstAsync().ToTask(CancellationToken);
    Assert.NotEmpty(state.CanVerifyStates[uri]);
    Assert.All(state.CanVerifyStates[uri].Values,
      verifiable => Assert.Equal(VerificationPreparationState.NotStarted, verifiable.PreparationProgress));
    telemetryPublisher.Verify(publisher => publisher.PublishTelemetry(It.IsAny<ImmutableDictionary<string, object>>()),
      Times.Never());
  }

  private sealed class PreflightOnlyWorkerPackage : IDisposable {
    private readonly string directory;
    public string Worker => Path.Combine(directory, "DafnyB3Host.dll");

    public PreflightOnlyWorkerPackage(string directory) {
      this.directory = directory;
      Directory.CreateDirectory(directory);
      var files = new Dictionary<string, string>();
      // These bytes satisfy production package-manifest preflight, not DLL validity.
      // Capability rejection and Never mode must prevent any worker execution.
      foreach (var name in new[] { "DafnyB3Host.dll", "B3Library.dll", "DafnyB3Protocol.dll" }) {
        var bytes = new byte[] { 1, 2, 3 };
        File.WriteAllBytes(Path.Combine(directory, name), bytes);
        files[name] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
      }
      var manifest = new WorkerManifest(Protocol.Version, WorkerPackage.UpstreamCommit, Protocol.NormalizerVersion,
        WorkerPackage.BootstrapCompiler, WorkerPackage.SourceFingerprint, files);
      File.WriteAllBytes(Path.Combine(directory, WorkerPackage.ManifestFileName),
        JsonSerializer.SerializeToUtf8Bytes(manifest, Protocol.JsonOptions));
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
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
