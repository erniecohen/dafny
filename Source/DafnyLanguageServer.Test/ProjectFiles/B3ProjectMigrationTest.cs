using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Extensions;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Util;
using Microsoft.Dafny.LanguageServer.Workspace;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Dafny.LanguageServer.IntegrationTest.ProjectFiles;

public class B3ProjectMigrationTest : ClientBasedLanguageServerTest {
  private readonly ControlledVerifier verifier = new();

  public B3ProjectMigrationTest(ITestOutputHelper output) : base(output) { }

  protected override void ServerOptionsAction(LanguageServerOptions serverOptions) {
    serverOptions.Services.AddSingleton<IProgramVerifier>(verifier);
  }

  [Fact]
  public async Task BackendChangeCancelsOldTasksAndRemapsBothOpenSources() {
    await SetUp(options => options.Set(ProjectManager.Verification, VerifyOnMode.Never));
    var directory = GetFreshTempPath();
    Directory.CreateDirectory(directory);
    try {
      var projectPath = Path.Combine(directory, DafnyProject.FileName);
      await File.WriteAllTextAsync(projectPath, "[options]\nverification-backend = \"boogie\"");
      var (first, second) = await OpenTwoSources(directory);
      var previous = (await Projects.GetProjectManager(first))!;
      var resolution = (await previous.Compilation.Resolution)!;
      await previous.VerifyEverythingAsync(null);
      var oldTasks = verifier.Items.Where(item => ReferenceEquals(item.Resolution, resolution)).ToList();
      Assert.Equal(2, oldTasks.Count);
      await Task.WhenAll(oldTasks.Select(item => item.Started.Task)).WaitAsync(CancellationToken);

      await FileTestExtensions.WriteWhenUnlocked(projectPath,
        "[options]\nverification-backend = \"b3\"\nverification-time-limit = 0");
      var replacement = (await Projects.GetProjectManager(first))!;
      Assert.NotSame(previous, replacement);
      Assert.True(previous.IsDisposed);
      Assert.Same(replacement, await Projects.GetProjectManager(second));
      Assert.Equal(B3OptionBag.Backend.B3,
        replacement.Compilation.Options.GetOrOptionDefault(B3OptionBag.VerificationBackend));
      await Task.WhenAll(oldTasks.Select(item => item.Cancelled.Task)).WaitAsync(CancellationToken);
      // Finish the cancelled generation after the replacement exists. Its observer is retired.
      foreach (var item in oldTasks) { item.CompleteCancelled(); }
      var newResolution = await replacement.Compilation.Resolution;
      Assert.True(newResolution!.HasErrors); // The explicitly invalid B3 limit must remain visible.
      var current = await replacement.GetStateAfterResolutionAsync();
      Assert.Contains(current.GetAllDiagnostics(), diagnostic =>
        diagnostic.Diagnostic.Message.Contains("B3 requires --verification-time-limit"));
    } finally {
      foreach (var item in verifier.Items) { item.CompleteCancelled(); }
      Directory.Delete(directory, true);
    }
  }

  [Fact]
  public async Task WorkerSettingChangeRemapsBothOpenSources() {
    await SetUp(options => options.Set(ProjectManager.Verification, VerifyOnMode.Never));
    var directory = GetFreshTempPath();
    Directory.CreateDirectory(directory);
    try {
      var projectPath = Path.Combine(directory, DafnyProject.FileName);
      await File.WriteAllTextAsync(projectPath,
        "[options]\nverification-backend = \"b3\"\nb3-worker = \"first-worker.dll\"\nverification-time-limit = 0");
      var (first, second) = await OpenTwoSources(directory);
      var previous = (await Projects.GetProjectManager(first))!;
      await previous.Compilation.Resolution;
      await FileTestExtensions.WriteWhenUnlocked(projectPath,
        "[options]\nverification-backend = \"b3\"\nb3-worker = \"second-worker.dll\"\nverification-time-limit = 0");
      var replacement = (await Projects.GetProjectManager(first))!;
      Assert.True(previous.IsDisposed);
      Assert.Same(replacement, await Projects.GetProjectManager(second));
      Assert.Equal(Path.Combine(directory, "second-worker.dll"),
        replacement.Compilation.Options.Get(B3OptionBag.Worker).FullName);
      Assert.Single(Projects.Managers);
      await replacement.Compilation.Resolution;
    } finally {
      Directory.Delete(directory, true);
    }
  }

  [Fact]
  public async Task RootChangesRemapRetainedSourcesAndDetachExcludedSource() {
    await SetUp(options => {
      options.Set(ProjectManager.Verification, VerifyOnMode.Never);
      options.WarnShadowing = false;
    });
    var directory = GetFreshTempPath();
    Directory.CreateDirectory(directory);
    try {
      var projectPath = Path.Combine(directory, DafnyProject.FileName);
      await File.WriteAllTextAsync(projectPath, "includes = [\"first.dfy\", \"second.dfy\", \"third.dfy\"]");
      var (first, second) = await OpenTwoSources(directory);
      var thirdDocument = CreateTestDocument("method Third() {}", Path.Combine(directory, "third.dfy"));
      await File.WriteAllTextAsync(thirdDocument.Uri.ToUri().LocalPath, thirdDocument.Text);
      await Projects.OpenDocument(thirdDocument);
      var third = new TextDocumentIdentifier(thirdDocument.Uri);
      var previous = (await Projects.GetProjectManager(first))!;
      Assert.Same(previous, await Projects.GetProjectManager(second));
      Assert.Same(previous, await Projects.GetProjectManager(third));
      await previous.Compilation.Resolution;
      await FileTestExtensions.WriteWhenUnlocked(projectPath,
        "includes = [\"first.dfy\", \"third.dfy\"]\n[options]\nwarn-shadowing = true");
      var replacement = (await Projects.GetProjectManager(first))!;
      Assert.NotSame(previous, replacement);
      Assert.True(previous.IsDisposed);
      Assert.Same(replacement, await Projects.GetProjectManager(third));
      Assert.True(replacement.Compilation.Options.WarnShadowing);
      var roots = await replacement.Compilation.RootFiles;
      Assert.Equal(new[] { first.Uri.ToUri(), third.Uri.ToUri() }, roots.Select(root => root.Uri));
      var detached = (await Projects.GetProjectManager(second))!;
      Assert.NotSame(replacement, detached);
      Assert.Equal(second.Uri.ToUri(), detached.Project.Uri);
      Assert.False(detached.Compilation.Options.WarnShadowing);
      Assert.Equal(second.Uri.ToUri(), Assert.Single(await detached.Compilation.RootFiles).Uri);
      Assert.Equal(2, Projects.Managers.Count());
      await replacement.Compilation.Resolution;
      await detached.Compilation.Resolution;

      // Only retained sources keep the replacement alive after the excluded source moves away.
      Projects.CloseDocument(first);
      Assert.False(replacement.IsDisposed);
      Projects.CloseDocument(third);
      Assert.True(replacement.IsDisposed);
      Assert.False(detached.IsDisposed);
      Projects.CloseDocument(second);
      Assert.True(detached.IsDisposed);
    } finally {
      Directory.Delete(directory, true);
    }
  }

  [Fact]
  public async Task DifferentProjectMoveReleasesTheActualSource() {
    await SetUp(options => options.Set(ProjectManager.Verification, VerifyOnMode.Never));
    var directory = GetFreshTempPath();
    var nested = Path.Combine(directory, "nested");
    Directory.CreateDirectory(nested);
    try {
      await File.WriteAllTextAsync(Path.Combine(directory, DafnyProject.FileName), "");
      var first = CreateTestDocument("method First() {}", Path.Combine(directory, "first.dfy"));
      var second = CreateTestDocument("method Second() {}", Path.Combine(nested, "second.dfy"));
      await File.WriteAllTextAsync(first.Uri.ToUri().LocalPath, first.Text);
      await File.WriteAllTextAsync(second.Uri.ToUri().LocalPath, second.Text);
      await Projects.OpenDocument(first);
      await Projects.OpenDocument(second);
      var previous = (await Projects.GetProjectManager(new TextDocumentIdentifier(first.Uri)))!;
      Assert.Same(previous, await Projects.GetProjectManager(new TextDocumentIdentifier(second.Uri)));
      await previous.Compilation.Resolution;
      await File.WriteAllTextAsync(Path.Combine(nested, DafnyProject.FileName), "");
      var replacement = (await Projects.GetProjectManager(new TextDocumentIdentifier(second.Uri)))!;
      Assert.NotSame(previous, replacement);
      Assert.False(previous.IsDisposed); // The first source remains in the parent project.
      Projects.CloseDocument(new TextDocumentIdentifier(first.Uri));
      Assert.True(previous.IsDisposed); // The moved source must no longer keep its old project open.
      await replacement.Compilation.Resolution;
    } finally {
      Directory.Delete(directory, true);
    }
  }

  private async Task<(TextDocumentIdentifier First, TextDocumentIdentifier Second)> OpenTwoSources(string directory) {
    var first = CreateTestDocument("method First() { assert true; }", Path.Combine(directory, "first.dfy"));
    var second = CreateTestDocument("method Second() { assert true; }", Path.Combine(directory, "second.dfy"));
    await File.WriteAllTextAsync(first.Uri.ToUri().LocalPath, first.Text);
    await File.WriteAllTextAsync(second.Uri.ToUri().LocalPath, second.Text);
    await Projects.OpenDocument(first);
    await Projects.OpenDocument(second);
    return (new TextDocumentIdentifier(first.Uri), new TextDocumentIdentifier(second.Uri));
  }

  // Lifecycle fixture: no solver is invoked. Each current source gets one controlled work item.
  private sealed class ControlledVerifier : IProgramVerifier {
    public ConcurrentBag<ControlledWorkItem> Items { get; } = new();

    public Task<IReadOnlyList<IVerificationWorkItem>> GetVerificationTasksAsync(IVerificationBackend backend,
      ResolutionResult resolution, ModuleDefinition moduleDefinition, CancellationToken cancellationToken) {
      var tasks = resolution.CanVerifies!.Values.SelectMany(tree => tree.Values)
        .Where(canVerify => canVerify.ContainingModule == moduleDefinition)
        .Select(canVerify => new ControlledWorkItem(resolution, canVerify)).ToList();
      foreach (var task in tasks) { Items.Add(task); }
      return Task.FromResult<IReadOnlyList<IVerificationWorkItem>>(tasks);
    }
  }

  private sealed class ControlledWorkItem : IVerificationWorkItem {
    private readonly Subject<VerificationStatus> updates = new();
    private VerificationStatus status = new VerificationStale();
    public ResolutionResult Resolution { get; }
    public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public VerificationIdentity Identity { get; }
    public VerificationSourceInfo Source { get; }
    public VerificationStatus CacheStatus => status;
    public bool IsIdle => status is VerificationStale or VerificationCompleted;

    public ControlledWorkItem(ResolutionResult resolution, ICanVerify canVerify) {
      Resolution = resolution;
      Identity = new VerificationIdentity(canVerify.FullDafnyName, "lifecycle:" + canVerify.FullDafnyName, 0, 0);
      var origin = new SourceOrigin(canVerify.StartToken, canVerify.EndToken);
      Source = new VerificationSourceInfo(canVerify, origin, origin,
        VerificationUnitKind.Other, canVerify.FullDafnyName, "controlled lifecycle", Array.Empty<Function>());
    }

    public IVerificationWorkItem FromSeed(int newSeed) => this;
    public IObservable<VerificationStatus> TryRun() => Observable.Create<VerificationStatus>(observer => {
      var subscription = updates.Subscribe(observer);
      status = new VerificationRunning();
      observer.OnNext(status);
      Started.TrySetResult(true);
      return subscription;
    });
    public void Cancel() => Cancelled.TrySetResult(true);
    public void CompleteCancelled() {
      status = new VerificationCompleted(new VerificationResult(VerificationOutcome.Cancelled,
        Array.Empty<DafnyDiagnostic>(), Array.Empty<VerificationAssertion>(), false,
        DateTime.UtcNow, TimeSpan.Zero, null, 0));
      updates.OnNext(status);
      updates.OnCompleted();
    }
  }
}
