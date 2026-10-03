// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
#nullable enable

using System;
using System.IO;
using System.Collections.Immutable;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Extensions;
using Microsoft.Dafny.LanguageServer.Language;
using Microsoft.Dafny.LanguageServer.Workspace;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Util;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;
using Serilog.Sinks.InMemory;
using Xunit;
using Xunit.Abstractions;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.Dafny.LanguageServer.IntegrationTest.Synchronization;

public class CardinalityCachingTest : ClientBasedLanguageServerTest {
  private InMemorySink sink = null!;

  protected override void ServerOptionsAction(LanguageServerOptions serverOptions) {
    sink = InMemorySink.Instance;
    var memoryLogger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.InMemory().CreateLogger();
    var factory = LoggerFactory.Create(builder => builder.AddSerilog(memoryLogger));
    serverOptions.Services.Replace(new ServiceDescriptor(typeof(ILoggerFactory), factory));
  }

  private int ApiCacheHits() => sink.Snapshot().LogEvents.Count(logEvent =>
    logEvent.RenderMessage().Contains("Resolution cache hit", StringComparison.Ordinal) &&
    logEvent.RenderMessage().Contains("Api", StringComparison.Ordinal));

  private static bool HasCode(Diagnostic diagnostic, string errorId) =>
    Equals(diagnostic.Code, new DiagnosticCode(errorId));

  private async Task AssertNoErrors(TextDocumentItem document) {
    await client.WaitForNotificationCompletionAsync(document.Uri, CancellationToken);
    Assert.True(await WaitUntilResolutionFinished(document, CancellationToken));
    // An initially error-free file need not publish an empty diagnostic list.
    // After an error, its clear notification must replace the previous list.
    var diagnostics = diagnosticsReceiver.GetLatestAndClearQueue(d => d.Uri == document.Uri);
    Assert.DoesNotContain(diagnostics?.Diagnostics.AsEnumerable() ?? Enumerable.Empty<Diagnostic>(),
      diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
  }

  private async Task AssertCardinalityError(TextDocumentItem document, string errorId) {
    await client.WaitForNotificationCompletionAsync(document.Uri, CancellationToken);
    Assert.False(await WaitUntilResolutionFinished(document, CancellationToken));
    var published = diagnosticsReceiver.GetLatestAndClearQueue(d => d.Uri == document.Uri);
    Assert.NotNull(published);
    Assert.Equal(document.Version, published.Version ?? document.Version);
    var errors = published.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
    Assert.NotEmpty(errors);
    Assert.All(errors, diagnostic => Assert.True(HasCode(diagnostic, errorId), diagnostic.Message));
  }

  [Fact]
  public async Task DownstreamEditsRevalidateCachedDependencies() {
    var directory = GetFreshTempPath();
    await CreateOpenAndWaitForResolve("", Path.Combine(directory, DafnyProject.FileName));
    await CreateOpenAndWaitForResolve("module Api { trait {:termination false} V {} }",
      Path.Combine(directory, "api.dfy"));
    await CreateOpenAndWaitForResolve("""
      module Storage {
        import A = Api
        type F = A.V -> bool
        datatype B = B(f: F)
      }
      """, Path.Combine(directory, "storage.dfy"));
    var clientDocument = await CreateOpenAndWaitForResolve("""
      module Client {
        import A = Api
        import S = Storage
        datatype D extends A.V = D(n: int)
      }
      """, Path.Combine(directory, "client.dfy"));
    await AssertNoErrors(clientDocument);
    var cacheHits = ApiCacheHits();

    ApplyChange(ref clientDocument, new Range(3, 0, 4, 0), "  datatype D extends A.V = D(b: S.B)\n");
    await AssertCardinalityError(clientDocument, "r_cardinality_expansive_cycle");
    Assert.True(ApiCacheHits() > cacheHits, "The unchanged Api module should have been reused.");

    ApplyChange(ref clientDocument, new Range(3, 0, 4, 0), "  datatype D extends A.V = D(n: int)\n");
    await AssertNoErrors(clientDocument);

    ApplyChange(ref clientDocument, new Range(3, 0, 4, 0), "  trait Middle<T> extends A.V {}\n");
    await AssertCardinalityError(clientDocument, "r_cardinality_unretained_parameter");

    ApplyChange(ref clientDocument, new Range(3, 0, 4, 0), "  datatype D extends A.V = D(n: int)\n");
    await AssertNoErrors(clientDocument);
  }

  [Fact]
  public async Task ImportedHelperAndExportEditsCannotLeaveAStaleAdmission() {
    var directory = GetFreshTempPath();
    await CreateOpenAndWaitForResolve("", Path.Combine(directory, DafnyProject.FileName));
    await CreateOpenAndWaitForResolve("module Api { trait {:termination false} V {} }",
      Path.Combine(directory, "api.dfy"));
    var storageDocument = await CreateOpenAndWaitForResolve("""
      module Storage {
        import A = Api
        export provides B, A
        type F = int
        datatype B = B(f: F)
      }
      """, Path.Combine(directory, "storage.dfy"));
    var clientDocument = await CreateOpenAndWaitForResolve("""
      module Client {
        import A = Api
        import S = Storage
        datatype D extends A.V = D(b: S.B)
      }
      """, Path.Combine(directory, "client.dfy"));
    await AssertNoErrors(clientDocument);
    var cacheHits = ApiCacheHits();

    ApplyChange(ref storageDocument, new Range(3, 0, 4, 0), "  type F = A.V -> bool\n");
    await client.WaitForNotificationCompletionAsync(storageDocument.Uri, CancellationToken);
    await AssertCardinalityError(clientDocument, "r_cardinality_expansive_cycle");
    Assert.True(ApiCacheHits() > cacheHits, "The unchanged Api module should have been reused.");

    ApplyChange(ref storageDocument, new Range(2, 0, 3, 0), "  export provides A reveals B, F\n");
    await client.WaitForNotificationCompletionAsync(storageDocument.Uri, CancellationToken);
    await AssertCardinalityError(clientDocument, "r_cardinality_expansive_cycle");

    ApplyChange(ref storageDocument, new Range(2, 0, 3, 0), "  export provides B, A\n");
    await client.WaitForNotificationCompletionAsync(storageDocument.Uri, CancellationToken);
    await AssertCardinalityError(clientDocument, "r_cardinality_expansive_cycle");

    ApplyChange(ref storageDocument, new Range(3, 0, 4, 0), "  type F = int\n");
    await client.WaitForNotificationCompletionAsync(storageDocument.Uri, CancellationToken);
    await AssertNoErrors(clientDocument);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task CancelledCacheEntryInvalidatesPreviouslyAdmittedProgram(bool waitForCache) {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.TypeSystemRefresh, true);
    options.Set(CommonOptionBag.GeneralTraits, CommonOptionBag.GeneralTraitsOptions.Datatype);
    var reporter = new BatchErrorReporter(options);
    var parse = await ProgramParser.Parse("trait V {} datatype D extends V = D(n: int)",
      new Uri("untitled:cached-cardinality.dfy"), reporter);
    await new ProgramResolver(parse.Program).Resolve(System.Threading.CancellationToken.None);
    Assert.Equal(0, reporter.ErrorCount);
    Assert.NotEmpty(BoogieGenerator.Translate(parse.Program, reporter).ToList());

    var cache = new ResolutionCache();
    var entered = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
    Task? holder = null;
    using var cancellation = new CancellationTokenSource();
    if (waitForCache) {
      holder = cache.Modules.UseAndPrune(async () => {
        entered.SetResult(0);
        await release.Task;
        return 0;
      }, System.Threading.CancellationToken.None);
      await entered.Task;
    } else {
      cancellation.Cancel();
    }
    var resolver = new CachingResolver(parse.Program, NullLogger<CachingResolver>.Instance,
      new NoTelemetryPublisher(), cache);
    try {
      var resolution = resolver.Resolve(cancellation.Token);
      if (waitForCache) {
        cancellation.Cancel();
      }
      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resolution);
      // Observe invalidated admission through the public translation precondition.
      Assert.Throws<InvalidOperationException>(() => BoogieGenerator.Translate(parse.Program, reporter).ToList());
    } finally {
      release.TrySetResult(0);
      if (holder != null) {
        await holder;
      }
    }
  }

  private sealed class NoTelemetryPublisher() : TelemetryPublisherBase(NullLogger<TelemetryPublisherBase>.Instance) {
    public override void PublishTelemetry(ImmutableDictionary<string, object> data) { }
  }

  public CardinalityCachingTest(ITestOutputHelper output) : base(output, LogLevel.Debug) { }
}
