#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Dafny;

/// <summary>
/// The prefix that a <see cref="ModuleNamingScope"/> puts into the names it generates.
///
/// A scope's names have the form <c>kind + Prefix + counter</c> (bound variables, locals, lets,
/// quantifier ids, proof-dependency ids) or <c>"_" + Prefix + counter + "_" + name</c> (compile
/// names), where the counter matches <c>[0-9_]*</c>. Counters restart in every scope, so two
/// modules of one program get distinct names only through distinct prefixes. Names of different
/// modules meet in the program's one proof-dependency table and in its compiled output.
///
/// Every prefix here is either empty or a letter, then letters and digits, then one <c>_</c>.
/// So the prefix of a generated name is the text up to the first <c>_</c> after its kind, and is
/// distinct from the empty prefix (whose counter starts with a digit). Two scopes therefore never
/// generate the same name unless their prefixes are equal, and these prefixes are equal only for
/// equal module names:
/// <list type="bullet">
/// <item><c>_module</c>, the default module, has the empty prefix, the spelling from before
///   module scopes;</item>
/// <item><c>_System</c> has <c>S_</c>, and a built-in scope <c>_System.N</c>, with N letters and
///   digits, has <c>S</c>N<c>_</c>;</item>
/// <item>a module name in the table has <c>M</c>, then the shortest start, at least
///   <see cref="MinimumLength"/> characters, of the base-32 SHA-256 digest of its UTF-8 name that
///   no other name in the table shares, then <c>_</c>. Names whose whole digests agree get the hex
///   form below instead;</item>
/// <item>any other name, including one of at most three UTF-8 bytes (whose hex is no longer than a
///   digest start), has <c>X</c>, then the lower-case hex of its UTF-8 bytes, then <c>_</c>.</item>
/// </list>
/// User module names cannot begin with <c>_</c>, so reserved names never stand for user modules.
/// A prefix depends only on the module's name and the set of names in the table, never on the
/// order in which scopes are opened.
/// </summary>
internal sealed class ModuleNamePrefixes {
  public const int MinimumLength = 6;
  public const string DefaultModuleName = "_module";
  private const string SystemModuleName = "_System";
  private const string Base32Alphabet = "abcdefghijklmnopqrstuvwxyz234567";

  public static readonly ModuleNamePrefixes Empty = new([], Digest, MinimumLength);

  private readonly Dictionary<string, string> prefixes = new(StringComparer.Ordinal);

  public static ModuleNamePrefixes For(IEnumerable<string> moduleNames) => new(moduleNames, Digest, MinimumLength);

  /// <summary>For tests: <paramref name="digest"/> must return a nonempty base-32 string.</summary>
  internal ModuleNamePrefixes(IEnumerable<string> moduleNames, Func<string, string> digest, int minimumLength) {
    var names = moduleNames.Where(name => !IsReserved(name)).Distinct(StringComparer.Ordinal).ToList();
    var digests = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var name in names) {
      var value = digest(name);
      if (value.Length == 0 || value.Any(c => !Base32Alphabet.Contains(c))) {
        throw new ArgumentException($"module-name digest '{value}' is not base 32", nameof(digest));
      }
      digests.Add(name, value);
    }
    Assign(names, Math.Max(1, minimumLength), digests);
  }

  private void Assign(List<string> group, int length, Dictionary<string, string> digests) {
    foreach (var shared in group.GroupBy(name => Start(digests[name], length), StringComparer.Ordinal)) {
      var members = shared.ToList();
      if (members.Count == 1) {
        prefixes.Add(members[0], "M" + shared.Key + "_");
      } else if (members.Any(name => digests[name].Length <= length)) {
        // The digests cannot tell these names apart: use the injective spelling.
        foreach (var name in members) { prefixes.Add(name, Hex(name)); }
      } else {
        Assign(members, length + 1, digests);
      }
    }
  }

  private static string Start(string digest, int length) => digest.Length <= length ? digest : digest[..length];

  public string Prefix(string moduleName) {
    if (moduleName == DefaultModuleName) { return ""; }
    if (moduleName == SystemModuleName) { return "S_"; }
    if (moduleName.StartsWith(SystemModuleName + ".", StringComparison.Ordinal)) {
      var suffix = moduleName[(SystemModuleName.Length + 1)..];
      if (suffix.Length != 0 && suffix.All(char.IsAsciiLetterOrDigit)) { return "S" + suffix + "_"; }
    }
    return prefixes.TryGetValue(moduleName, out var prefix) ? prefix : Hex(moduleName);
  }

  /// <summary>
  /// Names with their own prefix rule, which no table entry affects: reserved names, and names so
  /// short that their hex form is no longer than a digest prefix.
  /// </summary>
  private static bool IsReserved(string moduleName) => moduleName == DefaultModuleName ||
    moduleName == SystemModuleName || moduleName.StartsWith(SystemModuleName + ".", StringComparison.Ordinal) ||
    2 * Encoding.UTF8.GetByteCount(moduleName) <= MinimumLength;

  private static string Hex(string moduleName) =>
    "X" + Convert.ToHexStringLower(Encoding.UTF8.GetBytes(moduleName)) + "_";

  internal static string Digest(string moduleName) {
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(moduleName));
    var result = new StringBuilder((bytes.Length * 8 + 4) / 5);
    int buffer = 0, bits = 0;
    foreach (var b in bytes) {
      buffer = (buffer << 8) | b;
      bits += 8;
      while (bits >= 5) {
        bits -= 5;
        result.Append(Base32Alphabet[(buffer >> bits) & 31]);
      }
      buffer &= (1 << bits) - 1;
    }
    if (bits > 0) { result.Append(Base32Alphabet[(buffer << (5 - bits)) & 31]); }
    return result.ToString();
  }
}
