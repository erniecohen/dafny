#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Dafny;

/// <summary>
/// The stable per-module resolution identity of issue 261 (part of 241).
///
/// <see cref="ModuleResolutionKey"/> (L) is a semantic key: the module's source text, its options,
/// the producer version and the keys of its prerequisites. A resolved module also contains generated
/// names, whose prefix depends on the set of module names of the program (the coordinator decision of
/// 2026-11-18 on 241), which L leaves out. The identity adds that prefix, and it takes the identities of
/// the prerequisites, rather than their keys, so that a change to the context of a prerequisite changes
/// every dependent's identity too.
///
/// <code>
///   context(m)  = H("dafny-module-resolution-context-v2", prefix(m),
///                   [(role, name, identity(p)) for each direct prerequisite p])
///   identity(m) = H("dafny-module-resolution-identity-v2", L(m), context(m))
/// </code>
///
/// The identity does not depend on where the module is: not on its file's path, so two checkouts of
/// one tree share it, and not on where in its file the module starts, so inserting a line above it
/// keeps it. Two modules with the same identity resolve, with the same binary, to the same result up to
/// source coordinates: each coordinate of the result is that of a token of the module's own source or
/// of a prerequisite's, at the same offset from the start of its piece of source. A consumer that keeps
/// absolute coordinates must rebase them or key on them as well (see
/// docs/dev/design/261-resolution-identity.md). The encoding is length-delimited, ordered as written,
/// and versioned by the domain strings: a change to what it covers changes those strings.
/// </summary>
public static class ModuleResolutionIdentity {
  public const string ContextDomain = "dafny-module-resolution-context-v2";
  public const string IdentityDomain = "dafny-module-resolution-identity-v2";

  /// <summary>One direct prerequisite: its dependency role, full name and identity.</summary>
  public readonly record struct Prerequisite(string Role, string Name, string Identity);

  public static string Context(string namingPrefix, IEnumerable<Prerequisite> prerequisites) {
    var ordered = prerequisites.OrderBy(p => p.Role, StringComparer.Ordinal)
      .ThenBy(p => p.Name, StringComparer.Ordinal).ThenBy(p => p.Identity, StringComparer.Ordinal).ToArray();
    return Hash(writer => {
      writer.Write(ContextDomain);
      writer.Write(namingPrefix);
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

  private static string Hash(Action<BinaryWriter> write) {
    using var bytes = new MemoryStream();
    using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true)) {
      write(writer);
    }
    return Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray()));
  }
}
