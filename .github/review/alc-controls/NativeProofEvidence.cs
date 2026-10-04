using System.Globalization;
using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using static B3AlcGate.NativeProofSmokeControls;

namespace B3AlcGate;

internal sealed record NativeBatchEvidence(string Scope, int VcNum, string Outcome, long ResourceCount,
  NativeAssertionEvidence[] Assertions);
internal sealed record NativeAssertionEvidence(string Filename, int Line, int Column, string Description);
internal sealed record NativeCsvEvidence(string DisplayName, string Outcome, string Duration, long ResourceCount, string RandomSeed);
internal sealed record NativeCheckEvidence(int Index, int CommandIndex, string Reply, long ResourceReply,
  string LatestNegatedAssertionSha256, string ExactQueryPrefixSha256);
internal sealed record NativeVcEvidence(int ResetOrdinal, string Outcome, long FinalRawResourceReply,
  string InitialNegatedVcSha256, int[] CheckIndices);
internal sealed record NativeSolverLaunchEvidence(string Launch, string[] Arguments, int SolverExitCode,
  string CompletionSha256, string InputSha256, string OutputSha256, string ErrorSha256,
  long InputBytes, long OutputBytes, long ErrorBytes, int CommandCount, int Resets,
  string[] ActualSetOptions, NativeCheckEvidence[] Checks, NativeVcEvidence[] VcGroups);
internal sealed record NativeProofEvidenceReceipt(string JsonSha256, string CsvSha256, string CleanupSha256,
  NativeBatchEvidence[] Batches, NativeCsvEvidence[] CsvRows, NativeSolverLaunchEvidence[] SolverLaunches,
  int AssertionCount, int TargetRoutineBatchCount, int ProofCheckCount, int ProofVcGroupCount, int VersionProbeCount,
  long ResourceCount, bool FinalRawVcResourceRepliesMatchJsonMultiset, bool NativeQueryOrCostParityClaimed);

// Reads only completed, ownership-cleaned, hash-bound output. Never sends commands to
// a solver or canonicalizes/rewrites any input, output, option, query or resource count.
[SupportedOSPlatform("linux")]
internal static class NativeProofEvidence {
  private static readonly UTF8Encoding Utf8 = new(false, true);
  private const string CsvHeader = "TestResult.DisplayName,TestResult.Outcome,TestResult.Duration,TestResult.ResourceCount,RandomSeed";

  internal static NativeProofEvidenceReceipt Read(RunReceipt run, string jsonPath, string csvPath,
    string sourcePath, string targetRoutine, bool expectedFailure) {
    var jsonBytes = ReadBytes(jsonPath, 4194304);
    var csvBytes = ReadBytes(csvPath, 1048576);
    var batches = Batches(jsonBytes, sourcePath, targetRoutine, expectedFailure);
    var csv = Csv(csvBytes, batches);
    var cleanup = run.ProofCleanup ?? throw new SmokeFailure("proof-cleanup-evidence-required");
    var separator = cleanup.Evidence.LastIndexOf(" sha256=", StringComparison.Ordinal);
    Require(separator > 0, "cleanup-evidence-format");
    var path = cleanup.Evidence[..separator];
    var digest = cleanup.Evidence[(separator + " sha256=".Length)..];
    var cleanupBytes = ReadBytes(path, 4194304);
    Require(Digest(digest) && Sha256(cleanupBytes) == digest, "cleanup-receipt-pin-mismatch");
    using var document = JsonDocument.Parse(cleanupBytes, new JsonDocumentOptions { MaxDepth = 64 });
    var root = document.RootElement;
    Require(root.GetProperty("liveOwnedProcesses").GetInt32() == 0 && !root.GetProperty("populated").GetBoolean() &&
      root.GetProperty("finalMembers").GetArrayLength() == 0 && root.GetProperty("leafRemoved").GetBoolean() &&
      root.GetProperty("monitorStopped").GetBoolean() && root.GetProperty("admissionClosed").GetBoolean() &&
      root.GetProperty("fallbackRequests").GetArrayLength() == 0 && root.GetProperty("failures").GetArrayLength() == 0,
      "successful-exclusive-leaf-cleanup-required");
    var streams = root.GetProperty("streamReceipts").EnumerateArray().ToArray();
    Require(streams.Length is > 0 and <= 64 && streams.Length == cleanup.RecordedSolverGroups &&
      root.GetProperty("launches").GetArrayLength() == streams.Length &&
      streams.Select(s => s.GetProperty("launch").GetString()).Distinct(StringComparer.Ordinal).Count() == streams.Length,
      "complete-unique-owned-launch-ledger-required");
    var launches = new List<NativeSolverLaunchEvidence>();
    var versions = 0;
    foreach (var entry in streams) {
      var launch = entry.GetProperty("launch").GetString() ?? "";
      Require(launch.Length is > 0 and <= 128 && launch.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') &&
        entry.GetProperty("valid").GetBoolean() && entry.GetProperty("headerValid").GetBoolean(), "invalid-owned-launch-key");
      var folder = Path.Combine(Path.GetDirectoryName(path)!, "launches", launch);
      var completionBytes = ReadBytes(Path.Combine(folder, "complete.json"), 16384);
      var completionDigest = Sha256(completionBytes);
      Require(completionDigest == entry.GetProperty("completionSha256").GetString() &&
        completionBytes.Length == entry.GetProperty("completionBytes").GetInt32(), "owned-completion-pin-mismatch");
      using var completionDocument = JsonDocument.Parse(completionBytes);
      var completion = completionDocument.RootElement;
      Require(completion.GetProperty("token").GetString() == root.GetProperty("token").GetString() &&
        completion.GetProperty("errors").GetArrayLength() == 0, "owned-completion-header-mismatch");
      var readyBytes = ReadBytes(Path.Combine(folder, "ready.json"), 16384);
      using var readyDocument = JsonDocument.Parse(readyBytes);
      var ready = readyDocument.RootElement;
      Require(ready.GetProperty("token").GetString() == root.GetProperty("token").GetString(), "ready-token-mismatch");
      var wrapperPid = ready.GetProperty("wrapper").GetProperty("pid").GetInt32();
      var wrapperStart = ready.GetProperty("wrapper").GetProperty("startTime").GetUInt64();
      var owned = root.GetProperty("launches").EnumerateArray().Single(l =>
        l.GetProperty("wrapper").GetProperty("pid").GetInt32() == wrapperPid &&
        l.GetProperty("wrapper").GetProperty("startTime").GetUInt64() == wrapperStart);
      // ready.json is not independently trusted: arguments and image are obtained
      // from the launch object already bound by the returned cleanup hash.
      var arguments = owned.GetProperty("arguments").EnumerateArray().Select(a => a.GetString() ?? "").ToArray();
      var input = Stream(folder, "stdin", entry, completion);
      var output = Stream(folder, "stdout", entry, completion);
      var error = Stream(folder, "stderr", entry, completion);
      Require(error.Length == 0, "native-solver-stderr-not-empty");
      var exit = completion.GetProperty("solverExitCode").GetInt32();
      if (arguments.SequenceEqual(new[] { "-version" })) {
        Require(exit == 0 && input.Length == 0 && Utf8.GetString(output).TrimEnd('\r', '\n') == "Z3 version 5.1.0 - 64 bit",
          "actual-z3-version-probe-mismatch");
        versions++;
        launches.Add(new(launch, arguments, exit, completionDigest, Sha256(input), Sha256(output), Sha256(error),
          input.Length, output.Length, error.Length, 0, 0, [], [], []));
        continue;
      }
      Require(arguments.SequenceEqual(new[] { "-smt2", "-in" }), "ordinary-native-z3-launch-arguments-required");
      Require(exit is 0 or -15 or -9, "native-solver-exit-unexpected");
      var commands = SmtForms.Parse(input);
      var replies = SmtForms.Parse(output);
      Require(commands.Count > 0 && commands.All(c => c.IsList), "nonempty-native-smt-command-stream-required");
      Require(!replies.Any(r => r.Head == "error" || r.Atom is "unknown" or "unsupported"), "native-solver-error-or-unknown");
      // Keep all native checks, including additional counterexample checks for one
      // failing VC. The pinned native prover requests :rlimit immediately after each
      // sat/unsat response, using its actual :name ping/pong after each request.
      // Its batch count is the final raw reply for that VC, not a
      // delta or the sum of its individual replies. No resource count is rewritten.
      var protocolReplies = replies.Where(r => (r.Atom is "sat" or "unsat") || r.Head == ":rlimit").ToArray();
      Require(protocolReplies.Length > 0 && protocolReplies.Length % 2 == 0, "native-check-resource-reply-pairs-required");
      var results = new List<string>();
      var resources = new List<long>();
      for (var reply = 0; reply < protocolReplies.Length; reply += 2) {
        var verdict = protocolReplies[reply];
        var count = protocolReplies[reply + 1];
        Require(!verdict.IsList && (verdict.Atom is "sat" or "unsat") && count.IsList && count.Head == ":rlimit" &&
          long.TryParse(count.FirstArgument, NumberStyles.None, CultureInfo.InvariantCulture, out _),
          "native-check-resource-reply-order-invalid");
        results.Add(verdict.Atom!);
        resources.Add(long.Parse(count.FirstArgument!, CultureInfo.InvariantCulture));
      }
      Require(resources.All(value => value >= 0), "native-resource-reply-invalid");
      var checks = new List<NativeCheckEvidence>();
      var groups = new List<NativeVcEvidence>();
      var reset = 0;
      var prefixStart = 0;
      var groupFirstCheck = 0;
      string? assertion = null;
      void FinishGroup() {
        if (groupFirstCheck == checks.Count) { return; }
        var group = checks.Skip(groupFirstCheck).ToArray();
        // The ordinary counterexample loop preserves its first verdict. A Valid
        // first verdict ends the loop. An Invalid first verdict can be followed by
        // further sat replies and at most a final unsat reply after blocking a path.
        Require(reset > 0 && (group[0].Reply == "sat" || group.Length == 1) &&
          group.Take(group.Length - 1).All(check => check.Reply == "sat"), "native-counterexample-loop-shape-invalid");
        groups.Add(new(reset, group[0].Reply == "sat" ? "Invalid" : "Valid", group[^1].ResourceReply,
          group[0].LatestNegatedAssertionSha256, group.Select(check => check.Index).ToArray()));
        groupFirstCheck = checks.Count;
      }
      for (var index = 0; index < commands.Count; index++) {
        var command = commands[index];
        if (command.Head == "reset") {
          FinishGroup();
          reset++;
          prefixStart = command.Start;
          assertion = null;
        }
        if (command.Head == "assert" && command.ArgumentHead == "not") { assertion = RawDigest(input, command.Start, command.End); }
        if (command.Head is "check-sat" or "check-sat-assuming") {
          // This fixed scope supports the actual ordinary check-sat path only. The
          // complete raw stream and prefix hashes retain every blocking assertion
          // and every native :name ping. Nothing is stripped from captured bytes.
          Require(command.Head == "check-sat" && assertion != null && reset > 0 && index + 3 < commands.Count &&
            commands[index + 1].Head == "get-info" && commands[index + 1].FirstArgument == ":name" &&
            commands[index + 2].Head == "get-info" && commands[index + 2].FirstArgument == ":rlimit" &&
            commands[index + 3].Head == "get-info" && commands[index + 3].FirstArgument == ":name",
            "native-proof-check-ping-resource-request-sequence-invalid");
          var ordinal = checks.Count;
          Require(ordinal < results.Count && ordinal < resources.Count, "native-check-reply-ledger-incomplete");
          checks.Add(new(ordinal, index, results[ordinal], resources[ordinal], assertion!, RawDigest(input, prefixStart, command.End)));
        }
      }
      FinishGroup();
      Require(checks.Count > 0 && checks.Count == results.Count && checks.Count == resources.Count &&
        commands.Count(c => c.Head == "get-info" && c.FirstArgument == ":rlimit") == resources.Count &&
        groups.SelectMany(group => group.CheckIndices).SequenceEqual(checks.Select(check => check.Index)),
        "complete-native-query-resource-denominator-required");
      launches.Add(new(launch, arguments, exit, completionDigest, Sha256(input), Sha256(output), Sha256(error),
        input.Length, output.Length, error.Length, commands.Count, reset,
        commands.Where(c => c.Head == "set-option").Select(c => Utf8.GetString(input, c.Start, c.End - c.Start)).ToArray(),
        checks.ToArray(), groups.ToArray()));
    }
    var allChecks = launches.SelectMany(l => l.Checks).ToArray();
    var allGroups = launches.SelectMany(l => l.VcGroups).ToArray();
    Require(versions > 0 && allChecks.Length > 0 && allGroups.Length == batches.Length,
      "real-native-proof-batches-and-vc-groups-required");
    var match = allGroups.Select(group => (group.Outcome, group.FinalRawResourceReply)).Order().SequenceEqual(
      batches.Select(batch => (batch.Outcome, batch.ResourceCount)).Order());
    Require(match, "final-raw-vc-resource-and-outcome-replies-do-not-match-json-batches");
    var total = checked(batches.Sum(b => b.ResourceCount));
    Require(total > 0, "nonzero-measured-native-resource-denominator-required");
    return new(Sha256(jsonBytes), Sha256(csvBytes), digest, batches, csv, launches.ToArray(),
      batches.Sum(b => b.Assertions.Length), batches.Count(b => b.Scope.Contains(targetRoutine, StringComparison.Ordinal)),
      allChecks.Length, allGroups.Length, versions, total, match, false);
  }

  private static NativeBatchEvidence[] Batches(byte[] bytes, string sourcePath, string routine, bool expectedFailure) {
    using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 64 });
    var scopes = document.RootElement.GetProperty("verificationResults").EnumerateArray().ToArray();
    Require(scopes.Length is > 0 and <= 64, "nonempty-real-verification-scopes-required");
    var batches = new List<NativeBatchEvidence>();
    foreach (var scope in scopes) {
      var name = scope.GetProperty("name").GetString() ?? "";
      var result = scope.GetProperty("outcome").GetString();
      Require(name.Length is > 0 and <= 4096 && result is "Correct" or "Errors" &&
        !scope.TryGetProperty("traversalCompleted", out _), "native-boogie-scope-required");
      var parts = scope.GetProperty("vcResults").EnumerateArray().ToArray();
      Require(parts.Length is > 0 and <= 64, "zero-proof-batch-scope-refused");
      foreach (var part in parts) {
        var outcome = part.GetProperty("outcome").GetString() ?? "";
        Require(outcome is "Valid" or "Invalid" && !part.TryGetProperty("workId", out _), "unknown-or-nonnative-batch-refused");
        var assertions = part.GetProperty("assertions").EnumerateArray().Select(a => new NativeAssertionEvidence(
          a.GetProperty("filename").GetString() ?? "", a.GetProperty("line").GetInt32(), a.GetProperty("col").GetInt32(),
          a.GetProperty("description").GetString() ?? "")).ToArray();
        Require(assertions.Length is > 0 and <= 256 && assertions.All(a => a.Line > 0 &&
          a.Column >= 0 && SameSource(a.Filename, sourcePath)), "real-source-assertion-batch-required");
        var resources = part.GetProperty("resourceCount").GetInt64();
        Require(resources >= 0, "negative-native-resource-count");
        batches.Add(new(name, part.GetProperty("vcNum").GetInt32(), outcome, resources, assertions));
      }
      Require((result == "Errors") == parts.Any(p => p.GetProperty("outcome").GetString() == "Invalid"), "native-scope-outcome-inconsistent");
    }
    Require(batches.Count is > 0 and <= 256 && batches.Select(b => (b.Scope, b.VcNum)).Distinct().Count() == batches.Count &&
      batches.Any(b => b.Scope.Contains(routine, StringComparison.Ordinal)), "target-routine-proof-batch-required");
    if (expectedFailure) {
      // The source is a fixed unconditional lemma with assert false and no precondition;
      // a failing unrelated routine cannot satisfy this reachable false control.
      Require(batches.Any(b => b.Scope.Contains(routine, StringComparison.Ordinal) && b.Outcome == "Invalid" &&
        b.Assertions.Any(a => a.Line == 2)) && batches.Where(b => b.Outcome == "Invalid").All(b => b.Scope.Contains(routine, StringComparison.Ordinal)),
        "reachable-target-assertion-failure-required");
    } else { Require(batches.All(b => b.Outcome == "Valid"), "successful-control-has-failing-native-batch"); }
    return batches.ToArray();
  }

  private static NativeCsvEvidence[] Csv(byte[] bytes, NativeBatchEvidence[] batches) {
    var lines = Utf8.GetString(bytes).Split('\n').Select(l => l.TrimEnd('\r')).ToList();
    if (lines.Count > 0 && lines[^1] == "") { lines.RemoveAt(lines.Count - 1); }
    Require(lines.Count == batches.Length + 1 && lines[0] == CsvHeader, "actual-csv-batch-denominator-mismatch");
    var rows = lines.Skip(1).Select(line => {
      // These fixed identifiers contain no commas. The reviewed native writer itself
      // emits unquoted fields; a different wire format must be separately reviewed.
      var fields = line.Split(',');
      Require(fields.Length == 5 && fields[0].Length > 0 && fields[1] is "Passed" or "Failed" &&
        long.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out _), "native-csv-row-invalid");
      return new NativeCsvEvidence(fields[0], fields[1], fields[2], long.Parse(fields[3], CultureInfo.InvariantCulture), fields[4]);
    }).ToArray();
    Require(rows.Select(r => (r.Outcome, r.ResourceCount)).Order().SequenceEqual(
      batches.Select(b => (b.Outcome == "Valid" ? "Passed" : "Failed", b.ResourceCount)).Order()), "csv-json-batch-outcome-resource-mismatch");
    return rows;
  }

  private static byte[] Stream(string folder, string name, JsonElement receipt, JsonElement completion) {
    var bound = receipt.GetProperty("streams").EnumerateArray().Single(s => s.GetProperty("name").GetString() == name);
    var bytes = ReadBytes(Path.Combine(folder, name + ".bin"), 8388608);
    var digest = Sha256(bytes);
    var complete = completion.GetProperty("streams").GetProperty(name);
    Require(bound.GetProperty("valid").GetBoolean() && bound.GetProperty("bytes").GetInt64() == bytes.Length &&
      bound.GetProperty("sha256").GetString() == digest && complete.GetProperty("readBytes").GetInt64() == bytes.Length &&
      complete.GetProperty("writtenBytes").GetInt64() == bytes.Length && complete.GetProperty("readSha256").GetString() == digest &&
      complete.GetProperty("writtenSha256").GetString() == digest && (name == "stdin" || complete.GetProperty("end").GetString() == "eof"),
      "native-forwarded-byte-evidence-mismatch");
    return bytes;
  }
  private static bool SameSource(string logged, string source) {
    if (Uri.TryCreate(logged, UriKind.Absolute, out var uri) && uri.IsFile) { logged = uri.LocalPath; }
    // These controls have one source and no include. Native token filenames may be
    // absolute/file-URI or exactly the basename. Do not resolve another relative
    // directory against the host's working directory or accept a suffix match.
    if (!Path.IsPathRooted(logged)) { return logged == Path.GetFileName(source); }
    return Path.GetFullPath(logged) == Path.GetFullPath(source);
  }
  private static string RawDigest(byte[] bytes, int start, int end) => Convert.ToHexString(SHA256.HashData(bytes.AsSpan(start, end - start))).ToLowerInvariant();

  // Structural framing only. Start/End are raw byte offsets, so query-prefix hashes
  // preserve whitespace, comments, names and ordering. Quoted symbols and SMT-LIB
  // doubled-quote strings are consumed as one value. No parsed value is sent back.
  private sealed record SmtForm(int Start, int End, bool IsList, string? Atom, string? Head, string? FirstArgument, string? ArgumentHead);
  private sealed class SmtForms(byte[] bytes) {
    private int index;
    private int values;
    public static List<SmtForm> Parse(byte[] bytes) {
      _ = Utf8.GetString(bytes); // Refuse malformed UTF-8 rather than replace bytes.
      var reader = new SmtForms(bytes);
      var result = new List<SmtForm>();
      reader.Space();
      while (reader.index < bytes.Length) {
        Require(result.Count < 100000, "smt-form-count-bound");
        result.Add(reader.Value(0));
        reader.Space();
      }
      return result;
    }
    private void Space() {
      while (index < bytes.Length) {
        if (bytes[index] is 9 or 10 or 13 or 32) { index++; continue; }
        if (bytes[index] == (byte)';') {
          while (index < bytes.Length && bytes[index] != (byte)'\n') { index++; }
          continue;
        }
        break;
      }
    }
    private SmtForm Value(int depth) {
      Require(depth <= 512 && ++values <= 2000000 && index < bytes.Length, "smt-depth-or-value-bound");
      var start = index;
      if (bytes[index] == (byte)'(') {
        index++;
        var count = 0;
        SmtForm? first = null;
        SmtForm? argument = null;
        Space();
        while (index < bytes.Length && bytes[index] != (byte)')') {
          var value = Value(depth + 1);
          if (count == 0) { first = value; }
          if (count == 1) { argument = value; }
          count++;
          Space();
        }
        Require(index < bytes.Length && bytes[index] == (byte)')', "incomplete-native-smt-form");
        index++;
        return new(start, index, true, null, first?.Atom, argument?.Atom, argument?.Head);
      }
      Require(bytes[index] != (byte)')', "unexpected-native-smt-close");
      if (bytes[index] is (byte)'"' or (byte)'|') {
        var quote = bytes[index++];
        var ended = false;
        while (index < bytes.Length) {
          if (bytes[index++] != quote) { continue; }
          if (quote == (byte)'"' && index < bytes.Length && bytes[index] == quote) { index++; continue; }
          ended = true;
          break;
        }
        Require(ended, "incomplete-native-smt-quote");
      } else {
        while (index < bytes.Length && bytes[index] is not (9 or 10 or 13 or 32 or (byte)'(' or (byte)')' or (byte)';')) { index++; }
      }
      Require(index > start && index - start <= 65536, "native-smt-atom-bound");
      return new(start, index, false, Utf8.GetString(bytes, start, index - start), null, null, null);
    }
  }
}
