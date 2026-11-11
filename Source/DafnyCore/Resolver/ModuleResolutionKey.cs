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
    var current = options.Options.OptionArguments.Where(p => !ignored.Contains(p.Key.Name))
      .Select(p => new KeyValuePair<string, string>(p.Key.Name, Value(p.Value)));
    // Legacy parsing and module-local {:options} can write effective fields
    // without updating OptionArguments. Those fields still affect resolution,
    // diagnostics and generated declarations, so include their actual values.
    var legacy = new Dictionary<string, object?> {
      [nameof(options.DisallowSoundnessCheating)] = options.DisallowSoundnessCheating,
      [nameof(options.EnforcePrintEffects)] = options.EnforcePrintEffects,
      [nameof(options.ForbidNondeterminism)] = options.ForbidNondeterminism,
      [nameof(options.DefiniteAssignmentLevel)] = options.DefiniteAssignmentLevel,
      [nameof(options.WarnShadowing)] = options.WarnShadowing,
      [nameof(options.FunctionSyntax)] = options.FunctionSyntax,
      [nameof(options.QuantifierSyntax)] = options.QuantifierSyntax,
      [nameof(options.DisallowExterns)] = options.DisallowExterns,
      [nameof(options.DisallowConstructorCaseWithoutParentheses)] = options.DisallowConstructorCaseWithoutParentheses,
      [nameof(options.DeprecationNoise)] = options.DeprecationNoise,
      [nameof(options.DisableScopes)] = options.DisableScopes,
      [nameof(options.UseJavadocLikeDocstringRewriter)] = options.UseJavadocLikeDocstringRewriter,
      [nameof(options.OptimizeResolution)] = options.OptimizeResolution,
      [nameof(options.Induction)] = options.Induction,
      [nameof(options.InductionHeuristic)] = options.InductionHeuristic,
      [nameof(options.AutoTriggers)] = options.AutoTriggers,
      [nameof(options.RewriteFocalPredicates)] = options.RewriteFocalPredicates,
      [nameof(options.ignoreAutoReq)] = options.ignoreAutoReq,
      [nameof(options.TestContracts)] = options.TestContracts,
      [nameof(options.ArithMode)] = options.ArithMode,
      [nameof(options.DisableNLarith)] = options.DisableNLarith,
      [nameof(options.VerifyAllModules)] = options.VerifyAllModules,
      [nameof(options.Backend)] = options.Backend?.TargetId,
      ["SupportedNativeTypes"] = options.Backend?.SupportedNativeTypes.Order(StringComparer.Ordinal).ToArray(),
    };
    return current.Concat(legacy.Select(p => new KeyValuePair<string, string>("effective:" + p.Key, Value(p.Value))));
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
