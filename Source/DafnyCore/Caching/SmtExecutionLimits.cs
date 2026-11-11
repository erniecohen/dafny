#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Microsoft.Dafny.Caching;

/// <summary>Reads effective limit commands without mistaking comments, strings or quoted names for commands.</summary>
internal static class SmtExecutionLimits {
  internal readonly record struct Limits(uint Resource, uint Milliseconds, bool Supported);
  public static Limits Read(IEnumerable<string> chunks) {
    uint resource = 0;
    uint milliseconds = uint.MaxValue;
    var depth = 0;
    var comment = false;
    var quoted = false;
    var text = false;
    var token = new StringBuilder();
    var words = new List<string>();
    var supported = true;
    void EndToken() {
      if (token.Length == 0) { return; }
      if (depth == 1 && words.Count < 5) { words.Add(token.ToString()); }
      token.Clear();
    }
    foreach (var chunk in chunks) {
      // The transport sends every chunk as a complete line (SMTLibProcess.Send uses WriteLine).
      var input = chunk + "\n";
      for (var i = 0; i < input.Length; i++) {
        var c = input[i];
        if (comment) { if (c is '\n' or '\r') { comment = false; } continue; }
        if (quoted) { if (c == '|') { quoted = false; } continue; }
        if (text) {
          if (c == '"') { if (i + 1 < input.Length && input[i + 1] == '"') { i++; } else { text = false; } }
          continue;
        }
        if (c == ';') { EndToken(); comment = true; }
        else if (c == '|') { EndToken(); quoted = true; if (depth == 1 && words.Count < 5) { words.Add("<quoted>"); } }
        else if (c == '"') { EndToken(); text = true; if (depth == 1 && words.Count < 5) { words.Add("<string>"); } }
        else if (c == '(') { EndToken(); depth++; if (depth == 1) { words.Clear(); } }
        else if (c == ')') {
          EndToken();
          if (depth == 1 && words.Count > 0 && words[0] == "set-option") {
            var name = words.Count > 1 ? words[1] : "";
            if (name is ":timeout" or ":rlimit") {
              if (words.Count != 3 || !uint.TryParse(words[2], NumberStyles.None, CultureInfo.InvariantCulture, out var value)) {
                supported = false;
              } else if (name == ":timeout") { milliseconds = value; } else { resource = value; }
            } else if (name.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                       name.Contains("rlimit", StringComparison.OrdinalIgnoreCase) ||
                       name.Contains("thread", StringComparison.OrdinalIgnoreCase) || name == ":parallel.enable") {
              // Parallel and alternate deadline controls need a separately audited profile.
              supported = false;
            }
          }
          depth--;
          if (depth < 0) { supported = false; }
        } else if (char.IsWhiteSpace(c)) { EndToken(); }
        else { token.Append(c); }
      }
    }
    return new Limits(resource, milliseconds, supported && depth == 0 && !quoted && !text);
  }
}
