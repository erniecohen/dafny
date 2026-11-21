#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Dafny;

/// <summary>
/// The stable per-module resolution identity of issue 261 (part of 241).
///
/// <see cref="ModuleResolutionKey"/> (L) is a semantic key: the module's source text, its options,
/// the producer version and the keys of its prerequisites. A resolved module also contains what L
/// leaves out: generated names, whose prefix depends on the set of module names of the program
/// (the coordinator decision of 2026-11-18 on 241), and source coordinates, which L omits because
/// the source text alone does not fix where the module starts. The identity adds both, and it
/// takes the identities of the prerequisites, rather than their keys, so that a change to the
/// context of a prerequisite changes every dependent's identity too.
///
/// <code>
///   context(m)  = H("dafny-module-resolution-context-v1", prefix(m), location(m),
///                   [(role, name, identity(p)) for each direct prerequisite p])
///   identity(m) = H("dafny-module-resolution-identity-v1", L(m), context(m))
/// </code>
///
/// Two modules with the same identity resolve to the same result, generated names and source
/// coordinates included, with the same binary. This is the key that persistent caches of later
/// phases (the translation table of issue 261) use for a module's resolution result. The encoding
/// is length-delimited, ordered as written, and versioned by the domain strings: a change to what
/// it covers changes those strings.
/// </summary>
public static class ModuleResolutionIdentity {
  public const string ContextDomain = "dafny-module-resolution-context-v1";
  public const string IdentityDomain = "dafny-module-resolution-identity-v1";

  /// <summary>One direct prerequisite: its dependency role, full name and identity.</summary>
  public readonly record struct Prerequisite(string Role, string Name, string Identity);

  public static string Context(string namingPrefix, string location, IEnumerable<Prerequisite> prerequisites) {
    var ordered = prerequisites.OrderBy(p => p.Role, StringComparer.Ordinal)
      .ThenBy(p => p.Name, StringComparer.Ordinal).ThenBy(p => p.Identity, StringComparer.Ordinal).ToArray();
    return Hash(writer => {
      writer.Write(ContextDomain);
      writer.Write(namingPrefix);
      writer.Write(location);
      writer.Write(ordered.Length);
      foreach (var prerequisite in ordered) {
        writer.Write(prerequisite.Role);
        writer.Write(prerequisite.Name);
        writer.Write(prerequisite.Identity);
      }
    });
  }

  public static string Compute(string key, string context) => Hash(writer => {
    writer.Write(IdentityDomain);
    writer.Write(key);
    writer.Write(context);
  });

  /// <summary>
  /// Where the module's source starts: for each node whose text is hashed into its key, the URI,
  /// line, column and character offset of its first token. With the text, these fix the coordinates
  /// of every token of the module.
  /// </summary>
  public static string Location(IEnumerable<INode> nodes) {
    var text = new StringBuilder();
    foreach (var node in nodes) {
      var start = node.EntireRange.StartToken;
      var uri = start.Uri?.ToString() ?? "";
      text.Append(uri.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(uri)
        .Append(':').Append(start.line.ToString(CultureInfo.InvariantCulture))
        .Append(':').Append(start.col.ToString(CultureInfo.InvariantCulture))
        .Append(':').Append(start.pos.ToString(CultureInfo.InvariantCulture)).Append(';');
    }
    return text.ToString();
  }

  private static string Hash(Action<BinaryWriter> write) {
    using var bytes = new MemoryStream();
    using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true)) {
      write(writer);
    }
    return Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray()));
  }
}
