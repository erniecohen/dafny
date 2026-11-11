#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Dafny;

/// <summary>A versioned, length-delimited identity for a module's resolution inputs.</summary>
public static class ModuleResolutionKey {
  public readonly record struct Prerequisite(string Role, string Name, string Key);

  public static string Compute(string qualifiedName, string source, string version,
    IEnumerable<KeyValuePair<string, string>> options, IEnumerable<Prerequisite> prerequisites) {
    using var bytes = new MemoryStream();
    using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true)) {
      writer.Write("dafny-module-resolution-v1");
      writer.Write(qualifiedName);
      writer.Write(source);
      writer.Write(version);
      var orderedOptions = options.OrderBy(p => p.Key, StringComparer.Ordinal).ToArray();
      writer.Write(orderedOptions.Length);
      foreach (var option in orderedOptions) {
        writer.Write(option.Key);
        writer.Write(option.Value);
      }
      var orderedDependencies = prerequisites.OrderBy(p => p.Role, StringComparer.Ordinal)
        .ThenBy(p => p.Name, StringComparer.Ordinal).ThenBy(p => p.Key, StringComparer.Ordinal).ToArray();
      writer.Write(orderedDependencies.Length);
      foreach (var dependency in orderedDependencies) {
        writer.Write(dependency.Role);
        writer.Write(dependency.Name);
        writer.Write(dependency.Key);
      }
    }
    return Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray()));
  }

  internal static IEnumerable<KeyValuePair<string, string>> Options(DafnyOptions options) {
    // Execution, reporting, and lookup locations are not semantic resolution inputs.
    var ignored = new HashSet<string>(StringComparer.Ordinal) {
      "resolution-threads", "resolution-cache", "pipeline-profile", "bprint", "rprint", "print", "sprint", "pprint",
      "log-level", "log-location", "show-snippets", "use-basename-for-filename", "cores", "library",
      "log-format", "verification-coverage-report", "solver-log", "output", "progress"
    };
    return options.Options.OptionArguments.Where(p => !ignored.Contains(p.Key.Name))
      .Select(p => new KeyValuePair<string, string>(p.Key.Name, Value(p.Value)));
  }

  private static string Value(object? value) {
    if (value == null) {
      return "null";
    }
    if (value is string text) {
      return $"string:{text.Length}:{text}";
    }
    if (value is IEnumerable sequence) {
      return "[" + string.Join("", sequence.Cast<object?>().Select(v => {
        var item = Value(v);
        return $"{item.Length}:{item}";
      })) + "]";
    }
    return value.GetType().FullName + ":" + Convert.ToString(value, CultureInfo.InvariantCulture);
  }
}
