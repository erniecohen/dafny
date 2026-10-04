using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using DafnyB3Protocol;

namespace DafnyB3Host;

/// <summary>Checks the exact executable before any proof query is sent to it.</summary>
public static class SolverIdentity {
  private const int ProbeOutputBound = 16384;
  private const long ExecutableSizeBound = 512L * 1024 * 1024;

  public static async Task ValidateAsync(Configuration configuration, CancellationToken cancellationToken = default) {
    if (configuration.SolverVersion != "5.1.0" || configuration.TimeoutMilliseconds <= 0 ||
        configuration.SolverSha256 is not { Length: 64 } || !configuration.SolverSha256.All(Uri.IsHexDigit) ||
        string.IsNullOrEmpty(configuration.SolverExecutable) || !Path.IsPathFullyQualified(configuration.SolverExecutable)) {
      throw new InvalidDataException("B3 requires a resolved Z3 5.1.0 executable path, digest, and positive deadline");
    }
    cancellationToken.ThrowIfCancellationRequested();
    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    deadline.CancelAfter(TimeSpan.FromMilliseconds(Math.Min(configuration.TimeoutMilliseconds, 5000)));
    var token = deadline.Token;
    try {
      await using (var executable = new FileStream(configuration.SolverExecutable, FileMode.Open,
        FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan)) {
        if (!executable.CanSeek || executable.Length is <= 0 or > ExecutableSizeBound) {
          throw new InvalidDataException("B3 solver executable exceeds file bounds");
        }
        var digest = Convert.ToHexString(await SHA256.HashDataAsync(executable, token));
        if (!string.Equals(digest, configuration.SolverSha256, StringComparison.OrdinalIgnoreCase)) {
          throw new InvalidDataException("B3 solver executable digest mismatch");
        }
      }
      await ProbeAsync(configuration.SolverExecutable, token);
    } catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested) {
      throw new TimeoutException("B3 solver identity probe deadline exceeded", exception);
    }
  }

  private static async Task ProbeAsync(string executable, CancellationToken token) {
    var start = new ProcessStartInfo(executable) {
      UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
      CreateNoWindow = true
    };
    start.ArgumentList.Add("-version");
    using var process = new Process { StartInfo = start };
    var started = false;
    Task<string>? output = null;
    Task<string>? stderr = null;
    try {
      if (!process.Start()) { throw new IOException("B3 solver version probe failed to start"); }
      started = true;
      output = ReadAsync(process.StandardOutput, rejectOverflow: true, token);
      stderr = ReadAsync(process.StandardError, rejectOverflow: false, token);
      await Task.WhenAll(output, stderr, process.WaitForExitAsync(token)).WaitAsync(token);
      if (process.ExitCode != 0) {
        throw new InvalidDataException($"B3 solver version probe exited {process.ExitCode}: {await stderr}");
      }
      var version = (await output).Trim();
      if (version != "Z3 version 5.1.0 - 64 bit") {
        throw new InvalidDataException("B3 solver version mismatch: " + version);
      }
    } finally {
      if (started && !process.HasExited) {
        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(1));
      }
      // IO receives the same bounded cancellation token. Observe all reader failures.
      foreach (var reader in new[] { output, stderr }) {
        if (reader is null) { continue; }
        try { await reader.WaitAsync(TimeSpan.FromSeconds(1)); } catch { }
      }
    }
  }

  private static async Task<string> ReadAsync(StreamReader reader, bool rejectOverflow, CancellationToken token) {
    var text = new StringBuilder();
    var buffer = new char[1024];
    int count;
    while ((count = await reader.ReadAsync(buffer, token)) != 0) {
      if (rejectOverflow && text.Length + count > ProbeOutputBound) {
        throw new InvalidDataException("B3 solver version output exceeds bound");
      }
      text.Append(buffer, 0, count);
      if (text.Length > ProbeOutputBound) { text.Remove(0, text.Length - ProbeOutputBound); }
    }
    return text.ToString();
  }
}
