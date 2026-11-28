#nullable enable
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("IntegrationTests")]

namespace Microsoft.Dafny;

internal static class VerificationGc {
  internal const string RestartedVariable = "DAFNY_BATCH_GC_RESTARTED";
  private static readonly AsyncLocal<CliInvocation?> Invocation = new();

  internal static async Task<int> RunCli(string[] args) {
    var previous = Invocation.Value;
    var invocation = new CliInvocation(args);
    Invocation.Value = invocation;
    try {
      return await DafnyBackwardsCompatibleCli.MainWithWriters(
        invocation.Output, invocation.Error, Console.In, args);
    } finally {
      invocation.Publish();
      Invocation.Value = previous;
    }
  }

  internal static Task<int> RunVerification(DafnyOptions options, Func<Task<int>> verify) {
    var invocation = Invocation.Value;
    if (!options.GetOrOptionDefault(VerifyCommand.BatchGc)) {
      invocation?.Publish();
      return verify();
    }
    if (invocation == null) {
      return Reject(options, "--batch-gc requires the command-line executable so GC can be configured at startup.");
    }
    if (invocation.StartupMode == GCLatencyMode.Batch) {
      invocation.Publish();
      return verify();
    }
    if (invocation.Restarted) {
      invocation.Publish();
      return Reject(options, "--batch-gc could not disable background GC at startup; verification was not started.");
    }
    return Relaunch(options, invocation);
  }

  private static async Task<int> Reject(DafnyOptions options, string message) {
    await options.OutputWriter.Status("Error: " + message);
    return (int)ExitValue.PREPROCESSING_ERROR;
  }

  internal static ProcessStartInfo StartInfo(string host, string entry, string runtimeVersion, string[] args) {
    var start = new ProcessStartInfo(host) { UseShellExecute = false };
    if (Path.GetFileNameWithoutExtension(host).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) {
      start.ArgumentList.Add("exec");
      start.ArgumentList.Add("--fx-version");
      start.ArgumentList.Add(runtimeVersion);
      start.ArgumentList.Add(entry);
    }
    foreach (var arg in args) { start.ArgumentList.Add(arg); }
    foreach (var prefix in new[] { "DOTNET_", "COMPlus_" }) {
      start.Environment[prefix + "gcConcurrent"] = "0";
    }
    start.Environment[RestartedVariable] = "1";
    return start;
  }

  private static async Task<int> Relaunch(DafnyOptions options, CliInvocation invocation) {
    var entry = Assembly.GetEntryAssembly();
    if (entry?.GetName().Name is not ("Dafny" or "DafnyDriver") || Environment.ProcessPath == null) {
      invocation.Publish();
      return await Reject(options, "--batch-gc requires the command-line executable.");
    }
    using var child = new Process {
      StartInfo = StartInfo(Environment.ProcessPath, entry.Location, Environment.Version.ToString(), invocation.Args)
    };
    try {
      child.Start();
    } catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException) {
      invocation.Publish();
      return await Reject(options, "Could not start the verification process: " + exception.Message);
    }
    // The child repeats project validation. Only its preflight diagnostics are published.
    invocation.Discard();
    using var cancellation = new CancellationTokenSource();
    ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    EventHandler exiting = (_, _) => StopAndWait(child);
    Console.CancelKeyPress += cancel;
    AppDomain.CurrentDomain.ProcessExit += exiting;
    try {
      return await WaitForChild(child, cancellation.Token);
    } catch (OperationCanceledException) {
      return 130;
    } finally {
      Console.CancelKeyPress -= cancel;
      AppDomain.CurrentDomain.ProcessExit -= exiting;
    }
  }

  internal static async Task<int> WaitForChild(Process child, CancellationToken cancellation) {
    try {
      await child.WaitForExitAsync(cancellation);
      return child.ExitCode;
    } finally {
      StopAndWait(child);
    }
  }

  private static void StopAndWait(Process child) {
    try {
      if (!child.HasExited) { child.Kill(entireProcessTree: true); }
      child.WaitForExit();
    } catch (InvalidOperationException) {
      // A simultaneous process-exit callback may already have observed the exit.
    }
  }

  private sealed class CliInvocation {
    internal readonly string[] Args;
    internal readonly GCLatencyMode StartupMode = GCSettings.LatencyMode;
    internal readonly bool Restarted = Environment.GetEnvironmentVariable(RestartedVariable) == "1";
    private readonly Preflight preflight = new();
    internal readonly TextWriter Output;
    internal readonly TextWriter Error;
    internal CliInvocation(string[] args) {
      Args = args;
      Output = preflight.Writer(Console.Out);
      Error = preflight.Writer(Console.Error);
    }
    internal void Publish() => preflight.Publish();
    internal void Discard() => preflight.Discard();
  }

  // Both streams share a journal, preserving diagnostics' order when redirected
  // together. After the preflight decision writes stream directly to their targets.
  internal sealed class Preflight {
    private List<(TextWriter Target, string Text)>? pending = new();
    private bool discarded;
    internal bool Published => pending == null && !discarded;
    // Console writers complete async writes inline and serialize whole writes.
    // Keep those semantics: TextWriter's default async methods schedule work,
    // which can reorder unawaited diagnostics even after preflight publication.
    internal TextWriter Writer(TextWriter target) => TextWriter.Synchronized(new PreflightWriter(target, this));
    internal void Write(TextWriter target, string? value) {
      if (pending != null) { pending.Add((target, value ?? "")); }
      else if (!discarded) { target.Write(value); }
    }
    internal void Write(TextWriter target, char value) {
      if (pending != null) { pending.Add((target, value.ToString())); }
      else if (!discarded) { target.Write(value); }
    }
    internal void Write(TextWriter target, char[] buffer, int index, int count) {
      if (pending != null) { pending.Add((target, new string(buffer, index, count))); }
      else if (!discarded) { target.Write(buffer, index, count); }
    }
    internal void Publish() {
      if (pending == null) { return; }
      var buffered = pending;
      pending = null;
      foreach (var (target, text) in buffered) { target.Write(text); }
    }
    internal void Discard() {
      pending = null;
      discarded = true;
    }
  }

  internal sealed class PreflightWriter(TextWriter target, Preflight? preflight = null) : TextWriter {
    private readonly Preflight journal = preflight ?? new Preflight();
    public override Encoding Encoding => target.Encoding;
    public override void Write(char value) => journal.Write(target, value);
    public override void Write(string? value) => journal.Write(target, value);
    public override void Write(char[] buffer, int index, int count) => journal.Write(target, buffer, index, count);
    public override void Flush() { if (journal.Published) { target.Flush(); } }
    internal void Publish() => journal.Publish();
    internal void Discard() => journal.Discard();
  }
}
