using System.Diagnostics;
using System.Text;

/// <summary>
/// An adversarial SMT-LIB peer for the solver shim. It answers like Z3 with :print-success, except for the
/// single fault its mode names. It is a test fixture only and never part of a shipped assembly.
/// </summary>
public static class FakeSolver {
  public static async Task<int> RunAsync(string mode, string[] rest) {
    var stdout = Console.OpenStandardOutput();
    var stdin = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
    StreamWriter? record = mode == "record" ? new StreamWriter(rest[0]) { AutoFlush = true } : null;
    var echoes = 0;
    string? lastNonce = null;

    async Task Write(string text) {
      var bytes = Encoding.UTF8.GetBytes(text);
      await stdout.WriteAsync(bytes);
      await stdout.FlushAsync();
    }
    Task Line(string text) => Write(text + "\n");

    if (mode == "stale-startup") { await Line("unsat"); }
    if (mode == "stderr-flood") { await Console.OpenStandardError().WriteAsync(new byte[1024 * 1024]); }
    if (mode == "tree") {
      using var child = Process.Start(new ProcessStartInfo(Environment.ProcessPath!) {
        ArgumentList = { typeof(FakeSolver).Assembly.Location, "hang-child" }, UseShellExecute = false
      })!;
      File.WriteAllText(rest[0], $"{Environment.ProcessId},{child.Id}");
    }

    while (await stdin.ReadLineAsync() is { } command) {
      record?.WriteLine(command);
      if (command.StartsWith("(echo \"")) {
        var nonce = command["(echo \"".Length..^2];
        echoes++;
        if (mode == "reorder" && lastNonce == "reordered") { await Line("unsat"); lastNonce = nonce; continue; }
        lastNonce = nonce;
        await Line(mode == "quoted-echo" ? $"\"{nonce}\"" : mode == "wrong-echo" ? nonce + "x" : nonce);
        continue;
      }
      if (command == "(check-sat)") {
        switch (mode) {
          case "hang-check": case "tree": await Task.Delay(Timeout.Infinite); break;
          case "eof-check": return 0;
          case "truncated": await Write("(error \"abc"); return 0;
          case "truncated-atom": await Write("uns"); return 0;
          case "reorder": {
            // Answer with the next sync's nonce, then answer that sync with the check-sat result.
            var parts = lastNonce!.Split('_');
            await Line(string.Join('_', parts[..^1]) + "_" + (int.Parse(parts[^1]) + 1));
            lastNonce = "reordered";
            continue;
          }
        }
        var answer = mode switch {
          "sat" => "sat",
          "unknown-incomplete" => "unknown", "unknown-timeout" => "unknown", "unknown-canceled" => "unknown",
          "unknown-memout" => "unknown", "malformed-reason" => "unknown", "unknown-error" => "unknown",
          "unsat-string" => "\"unsat\"", "unsat-quoted" => "|unsat|", "unsat-upper" => "UNSAT", "unsat-list" => "(unsat)",
          "unsat-paren" => "unsat)", "unsat-adjacent" => "unsat|x|", "control" => "\u0001",
          "duplicate-unsat" => "unsat\nunsat", "error-after-unsat" => "unsat\n(error \"late\")",
          "error-check" => "(error \"line 1: unknown constant\")", "success-check" => "success",
          "oversized" => new string('a', 2 << 20), "deep" => new string('(', 100) + new string(')', 100),
          "invalid-utf8" => "(error \"ÿ\")",
          "commented" => "; a comment with ) and \" and (error \"x\")\n  unsat ; trailing comment )",
          _ => "unsat"
        };
        if (mode == "invalid-utf8") { await stdout.WriteAsync(new byte[] { (byte)'"', 0xff, (byte)'"', 10 }); continue; }
        await Line(answer);
        continue;
      }
      if (command == "(get-info :reason-unknown)") {
        await Line(mode switch {
          "unknown-incomplete" => "(:reason-unknown \"(incomplete quantifiers)\")",
          "unknown-timeout" => "(:reason-unknown \"timeout\")",
          "unknown-canceled" => "(:reason-unknown |canceled|)",
          "unknown-memout" => "(:reason-unknown memout)",
          "unknown-error" => "(error \"no reason\")",
          _ => "(:reason-unknown)"
        });
        continue;
      }
      var response = "success";
      if (command.StartsWith("(assert (not ")) {
        if (mode == "omit-goal-ack") { continue; }
        if (mode == "error-goal") { response = "(error \"goal rejected\")"; }
      } else if (command == "(push 1)") {
        if (mode == "error-push") { response = "(error \"push\")"; }
        if (mode == "stale-push") { response = "success\nunsat"; }
        if (mode == "omit-push-ack") { continue; }
      } else if (command == "(pop 1)") {
        if (mode == "error-pop") { response = "(error \"pop\")"; }
        if (mode == "omit-pop-ack") { continue; }
      } else if (command.StartsWith("(declare-")) {
        if (mode == "error-decl") { response = "(error \"declaration\")"; }
        if (mode == "duplicate-success") { response = "success\nsuccess"; }
        if (mode == "unsupported-decl") { response = "unsupported"; }
      } else if (command.StartsWith("(assert ")) {
        if (mode == "error-assert") { response = "(error \"assertion\")"; }
      } else if (command == "(set-option :print-success true)") {
        if (mode == "error-print-success") { response = "(error \"print-success\")"; }
        if (mode == "silent-startup") { continue; }
      } else if (command.StartsWith("(set-option :rlimit")) {
        if (mode == "error-rlimit") { response = "(error \"rlimit\")"; }
      } else if (command == "(exit)") {
        if (mode == "trailing-exit") { await Line("success\nunsat"); return 0; }
        await Line("success");
        return mode == "exit-nonzero" ? 1 : 0;
      }
      await Line(response);
    }
    return mode == "exit-nonzero" ? 1 : 0;
  }
}
