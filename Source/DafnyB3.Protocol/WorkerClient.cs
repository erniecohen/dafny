using System.Diagnostics;
using System.Text;

namespace DafnyB3;

public sealed record WorkerCommand(string Executable, IReadOnlyList<string> Arguments);
public sealed record TransportReceipt(string StandardError, bool StandardErrorTruncated);

/// <summary>
/// One process per request. A failed process is never reused. No CLI output scraping or fallback.
/// The executable and arguments are explicit and must identify a trusted worker implementation.
/// </summary>
public static class WorkerClient {
  public const int MaxStandardErrorBytes = 8192;

  public static async Task<TransportReceipt> CheckAsync(WorkerCommand command, Request request,
    TimeSpan deadline, CancellationToken cancellationToken = default) {
    Protocol.Validate(request);
    request = request with { Obligations = request.Obligations.ToArray() };
    if (deadline <= TimeSpan.Zero || deadline > TimeSpan.FromHours(1)) {
      throw new ArgumentOutOfRangeException(nameof(deadline));
    }
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeout.CancelAfter(deadline);
    using var process = new Process {
      StartInfo = new ProcessStartInfo(command.Executable) {
        UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
      }
    };
    foreach (var argument in command.Arguments) { process.StartInfo.ArgumentList.Add(argument); }
    var started = false;
    Task<TransportReceipt>? drain = null;
    Task? send = null;
    try {
      timeout.Token.ThrowIfCancellationRequested();
      started = process.Start();
      if (!started) { throw new ProtocolException("Worker did not start"); }
      drain = DrainAsync(process.StandardError.BaseStream, timeout.Token);
      // Writing and reading run concurrently: neither a full stdin nor stderr pipe can block reads.
      send = SendAsync(process.StandardInput.BaseStream, request, timeout.Token);
      var ledger = new ResponseLedger(request);
      while (await Protocol.ReadAsync<Response>(process.StandardOutput.BaseStream, timeout.Token) is { } response) {
        ledger.Accept(response);
      }
      await send;
      await process.WaitForExitAsync(timeout.Token);
      var receipt = await drain;
      if (process.ExitCode != 0) { throw new ProtocolException("Worker exited unsuccessfully"); }
      ledger.RequireProved();
      return receipt;
    } finally {
      // Cancel blocked pipe operations as well as every currently owned descendant on every exit.
      timeout.Cancel();
      if (started) {
        try {
          if (!process.HasExited) { process.Kill(entireProcessTree: true); }
        } catch (InvalidOperationException) { /* Already exited. */ }
        // Reaping has its own safety cap, independent of a cancelled request token.
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await process.WaitForExitAsync(cleanup.Token);
      }
      await ObserveAsync(send);
      await ObserveAsync(drain);
    }
  }

  private static async Task SendAsync(Stream stream, Request request, CancellationToken token) {
    await Protocol.WriteAsync(stream, request, token);
    stream.Close();
  }

  private static async Task ObserveAsync(Task? task) {
    if (task == null) { return; }
    try { await task.WaitAsync(TimeSpan.FromSeconds(5)); }
    catch (Exception) { /* Primary operation already failed; observe bounded cleanup I/O. */ }
  }

  private static async Task<TransportReceipt> DrainAsync(Stream stream, CancellationToken token) {
    var retained = new MemoryStream();
    var buffer = new byte[4096];
    var truncated = false;
    int count;
    while ((count = await stream.ReadAsync(buffer, token)) != 0) {
      var keep = Math.Min(count, MaxStandardErrorBytes - (int)retained.Length);
      retained.Write(buffer, 0, keep);
      truncated |= keep != count;
    }
    return new TransportReceipt(Encoding.UTF8.GetString(retained.ToArray()), truncated);
  }
}
