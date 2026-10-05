// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Extensions;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Util;
using Microsoft.Dafny.LanguageServer.Workspace;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using Xunit.Abstractions;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.Dafny.LanguageServer.IntegrationTest.Synchronization;

public class ExtendedNewtypeCachingTest : ClientBasedLanguageServerTest {
  private const string EnabledProject = "[options]\nextended-newtype-bases = true\ngeneral-newtypes = true\ntype-system-refresh = true\n";

  public override Task InitializeAsync() => Task.CompletedTask;

  private async Task Configure() => await SetUp(options => {
    options.Set(CommonOptionBag.ExtendedNewtypeBases, true);
    options.Set(CommonOptionBag.GeneralNewtypes, true);
    options.Set(CommonOptionBag.TypeSystemRefresh, true);
    options.Set(ProjectManager.Verification, VerifyOnMode.Never);
    options.Set(CommonOptionBag.UseStandardLibraries, false);
    options.ProverOptions.Add("SOLVER=noop");
  });

  private async Task AssertResolution(TextDocumentItem document, bool succeeds, string? message = null) {
    await client.WaitForNotificationCompletionAsync(document.Uri, CancellationToken);
    var resolved = await WaitUntilResolutionFinished(document, CancellationToken);
    var published = diagnosticsReceiver.GetLatestAndClearQueue(d => d.Uri == document.Uri);
    var errors = published?.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray()
      ?? Array.Empty<Diagnostic>();
    Assert.True(succeeds == resolved,
      $"Expected resolution success: {succeeds}; actual: {resolved}. " +
      string.Join(Environment.NewLine, errors.Select(diagnostic => diagnostic.Message)));
    if (succeeds) {
      Assert.Empty(errors);
    } else {
      Assert.NotEmpty(errors);
      Assert.Equal(document.Version, published!.Version ?? document.Version);
      Assert.Contains(errors, diagnostic => diagnostic.Message.Contains(message!, StringComparison.Ordinal));
    }
  }

  [Fact]
  public async Task ProjectOptionEditsCannotKeepAnEnabledAdmission() {
    await Configure();
    var directory = GetFreshTempPath();
    var project = await CreateOpenAndWaitForResolve(EnabledProject, Path.Combine(directory, DafnyProject.FileName));
    var source = await CreateOpenAndWaitForResolve("newtype N = ORDINAL", Path.Combine(directory, "source.dfy"));
    await AssertResolution(source, true);
    for (var repetition = 0; repetition < 2; repetition++) {
      ApplyChange(ref project, new Range(1, 0, 2, 0), "extended-newtype-bases = false\n");
      await client.WaitForNotificationCompletionAsync(project.Uri, CancellationToken);
      await AssertResolution(source, false, "must be based on");
      ApplyChange(ref project, new Range(1, 0, 2, 0), "extended-newtype-bases = true\n");
      await client.WaitForNotificationCompletionAsync(project.Uri, CancellationToken);
      await AssertResolution(source, true);
    }
  }

  [Fact]
  public async Task WitnessEditsRecomputeAutoInitialization() {
    await Configure();
    var directory = GetFreshTempPath();
    await CreateOpenAndWaitForResolve(EnabledProject, Path.Combine(directory, DafnyProject.FileName));
    const string use = " method NeedDefault<T(0)>() {} method Use() { NeedDefault<N>(); }";
    var current = "datatype D = D newtype N = D" + use;
    var document = await CreateOpenAndWaitForResolve(current, Path.Combine(directory, "witness.dfy"));
    await AssertResolution(document, true);
    for (var repetition = 0; repetition < 2; repetition++) {
      var updated = "datatype D = D newtype N = D witness *" + use;
      ApplyChange(ref document, new Range(0, 0, 0, current.Length), updated);
      current = updated;
      await AssertResolution(document, false, "support auto-initialization");
      updated = "datatype D = D newtype N = d: D | true witness D" + use;
      ApplyChange(ref document, new Range(0, 0, 0, current.Length), updated);
      current = updated;
      await AssertResolution(document, true);
      updated = "datatype D = D newtype N = D" + use;
      ApplyChange(ref document, new Range(0, 0, 0, current.Length), updated);
      current = updated;
      await AssertResolution(document, true);
    }
  }

  [Fact]
  public async Task ImportedBaseEditsCannotKeepOrdinalMembers() {
    await Configure();
    var directory = GetFreshTempPath();
    await CreateOpenAndWaitForResolve(EnabledProject, Path.Combine(directory, DafnyProject.FileName));
    const string ordinal = "module Api { newtype N = ORDINAL export API reveals N }";
    const string integer = "module Api { newtype N = int export API reveals N }";
    var api = await CreateOpenAndWaitForResolve(ordinal, Path.Combine(directory, "api.dfy"));
    var consumer = await CreateOpenAndWaitForResolve(
      "module Client { import A = Api`API lemma Use(n: A.N) { var offset := n.Offset; } }",
      Path.Combine(directory, "client.dfy"));
    await AssertResolution(consumer, true);
    for (var repetition = 0; repetition < 2; repetition++) {
      ApplyChange(ref api, new Range(0, 0, 0, ordinal.Length), integer);
      await client.WaitForNotificationCompletionAsync(api.Uri, CancellationToken);
      await AssertResolution(consumer, false, "member 'Offset'");
      ApplyChange(ref api, new Range(0, 0, 0, integer.Length), ordinal);
      await client.WaitForNotificationCompletionAsync(api.Uri, CancellationToken);
      await AssertResolution(consumer, true);
    }
  }

  [Fact]
  public async Task ExportEditsCannotKeepAnExposedRepresentation() {
    await Configure();
    var directory = GetFreshTempPath();
    await CreateOpenAndWaitForResolve(EnabledProject, Path.Combine(directory, DafnyProject.FileName));
    const string revealed = "module Api { newtype N = ORDINAL export API reveals N }";
    const string provided = "module Api { newtype N = ORDINAL export API provides N }";
    var api = await CreateOpenAndWaitForResolve(revealed, Path.Combine(directory, "api.dfy"));
    var consumer = await CreateOpenAndWaitForResolve(
      "module Client { import A = Api`API lemma Use(n: A.N) { var offset := n.Offset; } }",
      Path.Combine(directory, "client.dfy"));
    await AssertResolution(consumer, true);
    for (var repetition = 0; repetition < 2; repetition++) {
      ApplyChange(ref api, new Range(0, 0, 0, revealed.Length), provided);
      await client.WaitForNotificationCompletionAsync(api.Uri, CancellationToken);
      await AssertResolution(consumer, false, "member 'Offset'");
      ApplyChange(ref api, new Range(0, 0, 0, provided.Length), revealed);
      await client.WaitForNotificationCompletionAsync(api.Uri, CancellationToken);
      await AssertResolution(consumer, true);
    }
  }

  public ExtendedNewtypeCachingTest(ITestOutputHelper output) : base(output) { }
}
