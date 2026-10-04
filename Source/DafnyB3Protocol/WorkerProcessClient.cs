using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace DafnyB3Protocol;

/// <summary>Fresh worker per unit, bounded output and cancellation of the entire isolated process group.</summary>
public sealed class WorkerProcessClient(string executable, IReadOnlyList<string> arguments) {
  public async Task<Completion> RunAsync(Request request, CancellationToken cancellationToken) {
    ProtocolValidation.ValidateRequest(request);
    var start = new ProcessStartInfo(executable) {
      UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true,
      RedirectStandardError = true, CreateNoWindow = true
    };
    foreach (var argument in arguments) { start.ArgumentList.Add(argument); }
    using var process = new Process { StartInfo = start };
    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    if (request.Configuration.TimeoutMilliseconds > 0) {
      deadline.CancelAfter(request.Configuration.TimeoutMilliseconds);
    }
    var token = deadline.Token;
    var isolated = false;
    var stderr = new StringBuilder();
    Task? stderrTask = null;
    Completion? completion = null;
    Exception? error = null;
    try {
      token.ThrowIfCancellationRequested();
      if (!process.Start()) { throw new IOException("B3 worker failed to start"); }
      stderrTask = DrainErrorAsync(process.StandardError, stderr);
      var payload = JsonSerializer.SerializeToUtf8Bytes(request, Protocol.JsonOptions);
      if (payload.Length > Protocol.MaximumMessageBytes) { throw new InvalidDataException("B3 request exceeds message bound"); }
      await process.StandardInput.BaseStream.WriteAsync(payload, token);
      await process.StandardInput.WriteLineAsync();
      process.StandardInput.Close();
      var first = await ReadWorkerLineAsync(process, token);
      var started = JsonSerializer.Deserialize<WorkerStarted>(first, Protocol.JsonOptions)
        ?? throw new InvalidDataException("Missing B3 startup record");
      isolated = started.ProcessId == process.Id && started.IsolatedProcessGroup;
      if (started.Version != Protocol.Version || started.RequestId != request.RequestId ||
          started.Sequence != 0 || started.ProcessId != process.Id || !started.IsolatedProcessGroup) {
        throw new InvalidDataException("Mismatched or unisolated B3 worker startup");
      }
      var response = await ReadWorkerLineAsync(process, token);
      completion = JsonSerializer.Deserialize<Completion>(response, Protocol.JsonOptions)
        ?? throw new InvalidDataException("Missing B3 completion");
      ProtocolValidation.ValidateCompletion(request, completion);
      await process.WaitForExitAsync(token);
      if (process.ExitCode != 0) { throw new IOException($"B3 worker exited {process.ExitCode}"); }
      if (await process.StandardOutput.ReadAsync(new char[1], token) != 0) {
        throw new InvalidDataException("Unexpected records after B3 completion");
      }
    } catch (Exception exception) {
      error = exception;
    } finally {
      try {
        if (isolated) { UnixProcessGroup.Kill(process.Id); }
        if (HasStarted(process) && !process.HasExited) { process.Kill(entireProcessTree: true); }
        if (HasStarted(process) && !process.HasExited) {
          await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
      } catch (Exception cleanupError) {
        error ??= new IOException("B3 process cleanup failed", cleanupError);
      }
      if (stderrTask is not null) {
        try { await stderrTask.WaitAsync(TimeSpan.FromSeconds(2)); }
        catch (Exception drainError) { error ??= new IOException("B3 stderr cleanup failed", drainError); }
      }
    }
    // The deadline and caller cancellation also cover process/pipe cleanup.
    if (token.IsCancellationRequested) { error ??= new OperationCanceledException(token); }
    if (error is null && completion is not null) { return completion; }
    var outcome = cancellationToken.IsCancellationRequested ? Outcome.Cancelled :
      error is OperationCanceledException ? Outcome.TimedOut : Outcome.ToolError;
    var reason = error?.Message ?? "Missing B3 completion";
    if (stderr.Length > 0) { reason += ": " + stderr; }
    return new Completion(Protocol.Version, request.RequestId, request.ProgramHash, request.UnitId,
      request.B3Commit, false, outcome, Array.Empty<Attempt>(), reason, request.WorkerFingerprint);
  }

  private static async Task<string> ReadWorkerLineAsync(Process process, CancellationToken token) {
    var record = ReadBoundedLineAsync(process.StandardOutput, token);
    var exited = process.WaitForExitAsync(token);
    if (await Task.WhenAny(record, exited) == exited && !record.IsCompleted) {
      // Descendants may still hold the pipe open after a worker crash. Allow buffered data to drain,
      // then use the worker exit as evidence of missing completion, independently of the unit deadline.
      try { return await record.WaitAsync(TimeSpan.FromMilliseconds(500), token); }
      catch (TimeoutException) { throw new IOException("B3 worker exited before completing its response"); }
    }
    return await record;
  }

  private static bool HasStarted(Process process) {
    try { _ = process.Id; return true; } catch (InvalidOperationException) { return false; }
  }
  private static async Task DrainErrorAsync(StreamReader reader, StringBuilder retained) {
    var buffer = new char[4096];
    int count;
    while ((count = await reader.ReadAsync(buffer)) > 0) {
      var keep = Math.Min(count, 65536 - retained.Length);
      if (keep > 0) { retained.Append(buffer, 0, keep); }
    }
  }
  public static async Task<string> ReadBoundedLineAsync(StreamReader reader, CancellationToken token) {
    var result = new StringBuilder();
    var buffer = new char[1];
    while (await reader.ReadAsync(buffer, token) != 0) {
      if (buffer[0] == '\n') { return result.ToString(); }
      if (result.Length >= Protocol.MaximumMessageBytes) { throw new InvalidDataException("B3 response exceeds message bound"); }
      result.Append(buffer[0]);
    }
    throw new EndOfStreamException("B3 stream ended without a complete record");
  }
}

/// <summary>The worker creates its group before starting any solver; the supervisor also cleans it after worker crashes.</summary>
public static class UnixProcessGroup {
  [DllImport("libc", SetLastError = true)] private static extern int setpgid(int pid, int group);
  [DllImport("libc", SetLastError = true)] private static extern int kill(int group, int signal);
  public static bool IsolateCurrentProcess() {
    if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) { return false; }
    return setpgid(0, 0) == 0;
  }
  public static void Kill(int processId) {
    if (processId <= 1) { throw new InvalidOperationException("Invalid isolated worker identity"); }
    if (kill(-processId, 9) != 0 && Marshal.GetLastPInvokeError() != 3) {
      throw new IOException($"Cannot clean B3 process group: OS error {Marshal.GetLastPInvokeError()}");
    }
  }
}
