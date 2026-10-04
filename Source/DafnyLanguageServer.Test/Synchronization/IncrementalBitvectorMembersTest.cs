using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Util;
using Microsoft.Dafny.LanguageServer.Workspace;
using Microsoft.Dafny.LanguageServer.Workspace.Notifications;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using Xunit.Abstractions;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.Dafny.LanguageServer.IntegrationTest.Synchronization;

[Collection("Sequential Collection")]
public class IncrementalBitvectorMembersTest : ClientBasedLanguageServerTest {
  public IncrementalBitvectorMembersTest(ITestOutputHelper output) : base(output) { }
  public override Task InitializeAsync() => Task.CompletedTask;

  [Theory]
  [InlineData("newtype Foo = bv64")]
  [InlineData("newtype V = bv64\ntrait Op<T> { function op2(t0: T, t1: T): T }")]
  [InlineData("const c := (1 as bv64).RotateRight(1)")]
  [InlineData("newtype V = bv32\nconst c := (1 as bv32).RotateLeft(1)")]
  [InlineData("newtype V = bv63\nconst c := (1 as bv63).RotateRight(1)")]
  [InlineData("newtype V = bv65\nconst c := (1 as bv65).RotateLeft(1)")]
  public async Task WhitespaceEditsPreserveInheritedAndRotationMembers(string source) {
    await SetUp(options => {
      options.Set(CommonOptionBag.TypeSystemRefresh, true);
      options.Set(CommonOptionBag.GeneralTraits, CommonOptionBag.GeneralTraitsOptions.Datatype);
      options.Set(CommonOptionBag.GeneralNewtypes, true);
      options.Set(CommonOptionBag.UseStandardLibraries, false);
      options.Set(ProjectManager.Verification, VerifyOnMode.Change);
      if (Environment.GetEnvironmentVariable("Z3") is { } solver) {
        options.Set(BoogieOptionBag.SolverPath, new FileInfo(solver));
      }
      options.Set(BoogieOptionBag.SolverResourceLimit, 200000U);
      options.Set(BoogieOptionBag.VerificationTimeLimit, 10U);
    });
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
    var document = CreateAndOpenTestDocument(source, "incremental-bitvector-members.dfy");
    for (var edit = 0; edit < 5; edit++) {
      if (edit == 3) {
        // Add another lazily created width to an already populated cache, then edit again.
        ApplyChange(ref document, new Range(0, 0, 0, 0),
          "newtype Later = bv17\nconst rotation := (1 as bv17).RotateLeft(1)\n");
      } else if (edit != 0) {
        ApplyChange(ref document, new Range(0, 0, 0, 0), "\n");
      }
      Assert.True(await WaitUntilResolutionFinished(document, timeout.Token));
      var status = verificationStatusReceiver.GetLatestAndClearQueue(notification =>
        notification.Uri == document.Uri && notification.Version == document.Version);
      while (status == null || status.Version != document.Version || status.Uri != document.Uri ||
             status.NamedVerifiables.Count == 0 || status.NamedVerifiables.Any(task => task.Status < PublishedVerificationStatus.Error)) {
        status = await verificationStatusReceiver.AwaitNextNotificationAsync(timeout.Token);
      }
      Assert.All(status.NamedVerifiables, task => Assert.Equal(PublishedVerificationStatus.Correct, task.Status));
      Assert.DoesNotContain(diagnosticsReceiver.History.Where(notification =>
        notification.Uri == document.Uri && notification.Version == document.Version).SelectMany(notification => notification.Diagnostics),
        diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    // A missing member must still be diagnosed after the successful cached resolutions.
    ApplyChange(ref document, new Range(0, 0, 0, 0), "const invalid := (1 as bv64).MissingMember(1)\n");
    Assert.False(await WaitUntilResolutionFinished(document, timeout.Token));
    var diagnostics = diagnosticsReceiver.GetLatestAndClearQueue(notification =>
      notification.Uri == document.Uri && notification.Version == document.Version);
    while (diagnostics == null || diagnostics.Uri != document.Uri || diagnostics.Version != document.Version) {
      diagnostics = await diagnosticsReceiver.AwaitNextNotificationAsync(timeout.Token);
    }
    Assert.Contains(diagnostics.Diagnostics, diagnostic =>
      diagnostic.Message.Contains("member 'MissingMember' does not exist"));
  }
}
