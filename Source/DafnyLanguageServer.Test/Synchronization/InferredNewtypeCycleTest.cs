using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Util;
using Microsoft.Dafny.LanguageServer.Workspace;
using Microsoft.Dafny.LanguageServer.Workspace.Notifications;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Dafny.LanguageServer.IntegrationTest.Synchronization;

[Collection("Sequential Collection")]
public class InferredNewtypeCycleTest : ClientBasedLanguageServerTest {
  private const string Valid = """
    newtype A = b | P(b)
    newtype B = a: int | true
    predicate P(b: B) { true }
    """;
  private const string Cyclic = """
    newtype A = b | P(b)
    newtype B = a: A | true
    predicate P(b: B) { true }
    """;
  private const string IndependentValid = "function Echo(x: int): int { x + 1 }";
  private const string IndependentError = "function Echo(x: int): int { missing }";

  // Each theory invocation creates just one server, with its resolver mode selected explicitly.
  public override Task InitializeAsync() => Task.CompletedTask;

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task EditCycleAndRecoverWithoutAffectingAnotherDocument(bool refreshed) {
    await SetUp(options => {
      options.Set(CommonOptionBag.TypeSystemRefresh, refreshed);
      options.Set(CommonOptionBag.GeneralNewtypes, false);
      options.Set(CommonOptionBag.GeneralTraits, CommonOptionBag.GeneralTraitsOptions.Legacy);
      options.Set(CommonOptionBag.UseStandardLibraries, false);
      options.Set(ProjectManager.Verification, VerifyOnMode.Never);
      // The server validates solver options during resolution. This inert setting skips
      // executable discovery; automatic verification is disabled and no solver is started.
      options.ProverOptions.Add("SOLVER=noop");
    });
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var cancellationToken = timeout.Token;
    var document = CreateAndOpenTestDocument(Valid, "inferred-newtype-cycle.dfy");
    await RequireResolution(document, true, cancellationToken);
    var independent = CreateAndOpenTestDocument(IndependentValid, "independent.dfy");
    await RequireResolution(independent, true, cancellationToken);

    for (var repetition = 0; repetition < 2; repetition++) {
      ApplyChange(ref document, null, Cyclic);
      var cycleDiagnostics = await DiagnosticsForVersion(document, false, cancellationToken);
      Assert.Contains(cycleDiagnostics.Diagnostics, diagnostic =>
        diagnostic.Severity == DiagnosticSeverity.Error &&
        diagnostic.Source == MessageSource.Resolver.ToString() &&
        (diagnostic.Message.StartsWith("cycle among redirecting types (newtypes, subset types, type synonyms):") ||
         refreshed && diagnostic.Message.StartsWith("Cyclic dependency among declarations:")) &&
        diagnostic.Message.Contains(" -> "));

      // Resolve and recover an unrelated file while the first file still contains its cycle.
      ApplyChange(ref independent, null, IndependentError);
      var independentDiagnostics = await DiagnosticsForVersion(independent, false, cancellationToken);
      Assert.Contains(independentDiagnostics.Diagnostics, diagnostic =>
        diagnostic.Message.Contains("unresolved identifier: missing"));
      Assert.DoesNotContain(independentDiagnostics.Diagnostics, diagnostic =>
        diagnostic.Message.Contains("cycle", StringComparison.OrdinalIgnoreCase));
      ApplyChange(ref independent, null, IndependentValid);
      var independentRecovery = await DiagnosticsForVersion(independent, true, cancellationToken);
      Assert.Empty(independentRecovery.Diagnostics);

      ApplyChange(ref document, null, Valid);
      var recovery = await DiagnosticsForVersion(document, true, cancellationToken);
      Assert.Empty(recovery.Diagnostics);
    }
  }

  private async Task RequireResolution(TextDocumentItem document, bool successful,
    CancellationToken cancellationToken) {
    // Permit the helper to return an exceptional status so an assertion can include its diagnostics.
    // Only the requested normal resolution status can satisfy this test.
    await WaitUntilResolutionFinished(document, cancellationToken, allowException: true);
    var actual = compilationStatusReceiver.History.Last(notification =>
      notification.Uri == document.Uri && notification.Version == document.Version);
    var expected = successful ? CompilationStatus.ResolutionSucceeded : CompilationStatus.ResolutionFailed;
    if (actual.Status != expected) {
      var diagnostics = string.Join(Environment.NewLine, diagnosticsReceiver.History.Select(notification =>
        $"{notification.Uri}, version {notification.Version}: {PrintDiagnostics(notification.Diagnostics)}"));
      Assert.True(false, $"Expected {expected} for {document.Uri}, version {document.Version}; " +
                         $"received {actual.Status}, message: {actual.Message}. Diagnostics:\n{diagnostics}");
    }
    Assert.Equal(document.Uri, actual.Uri);
    Assert.Equal(document.Version, actual.Version);
  }

  private async Task<PublishDiagnosticsParams> DiagnosticsForVersion(TextDocumentItem document,
    bool successful, CancellationToken cancellationToken) {
    await RequireResolution(document, successful, cancellationToken);
    var diagnostics = diagnosticsReceiver.GetLatestAndClearQueue(notification =>
      notification.Uri == document.Uri && notification.Version == document.Version);
    while (diagnostics == null) {
      var notification = await diagnosticsReceiver.AwaitNextNotificationAsync(cancellationToken);
      if (notification.Uri == document.Uri && notification.Version == document.Version) {
        diagnostics = notification;
      }
    }
    // A migrated or late notification from an older edit must not satisfy the test.
    Assert.Equal(document.Uri, diagnostics.Uri);
    Assert.Equal(document.Version, diagnostics.Version);
    return diagnostics;
  }

  public InferredNewtypeCycleTest(ITestOutputHelper output) : base(output) {
  }
}
