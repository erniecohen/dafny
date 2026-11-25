#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Dafny;

/// <summary>A versioned, length-delimited identity for a module's resolution inputs.</summary>
public static class ModuleResolutionKey {
  public readonly record struct Prerequisite(string Role, string Name, string Key);

  // FileVersion is only 4.11.0.0 on this line. InformationalVersion includes
  // the fork revision; MVID also distinguishes local builds of uncommitted code
  // or builds that omit revision metadata. Both are fixed assembly metadata.
  public static string DafnyVersion { get; } =
    typeof(ModuleResolutionKey).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion +
    ";resolver-mvid:" + typeof(ModuleResolutionKey).Module.ModuleVersionId.ToString("D");

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

  /// <summary>
  /// The options left out of every module's resolution key, each with the reason it cannot change a module's
  /// resolution result. Every other option is in the key (default-deny): a new option is a key input until it is
  /// listed here. DafnyDriver.Test checks that each name is a registered option and that toggling any other option
  /// changes the digest.
  /// </summary>
  public static IReadOnlyDictionary<string, string> IgnoredOptions { get; } = new Dictionary<string, string>(StringComparer.Ordinal) {
    ["resolution-threads"] = "scheduling only: sequential and parallel resolution produce the same results",
    ["resolution-cache"] = "switches this cache; a reused result equals a fresh one",
    ["resolution-cache-check"] = "compares reused results with fresh ones; changes no result",
    ["translation-cache"] = "a later stage's cache",
    ["translation-workers"] = "a later stage's scheduling: any number of workers commits the same translations",
    ["translation-workers-stress"] = "test-only delays of a later stage's scheduling",
    ["persistent-translation"] = "a later stage's cache",
    ["persistent-translation-audit"] = "checks a later stage's cache; changes no result",
    ["verification-cache"] = "the store's root, not an input; keys never name a location",
    ["verification-cache-solver-digest"] = "declares which solver images the result cache accepts; the digest of the image run is in its keys",
    ["cache-partition"] = "store accounting only",
    ["cache-partition-budget"] = "store retention only",
    ["cache-size-cap"] = "store retention only",
    ["cache-orphan-days"] = "store retention only",
    ["pipeline-profile"] = "profiling output",
    ["pipeline-profile-heap"] = "profiling output",
    ["pipeline-profile-gc-events"] = "profiling output",
    ["pipeline-profile-hold"] = "profiling output",
    ["bprint"] = "writes the Boogie program of a later stage",
    ["rprint"] = "prints the resolved program; does not change it",
    ["print"] = "prints the parsed program; does not change it",
    ["sprint"] = "prints the program; does not change it",
    ["pprint"] = "prints the program; does not change it",
    ["log-level"] = "language server logging",
    ["log-location"] = "language server logging",
    ["show-snippets"] = "how diagnostics are rendered at report time",
    ["use-basename-for-filename"] = "how file names are rendered at report time; keys and snapshots hold no file names",
    ["cores"] = "verification parallelism",
    ["library"] = "which files are libraries reaches the key as each declaration's effective verification membership",
    ["log-format"] = "verification logs",
    ["verification-coverage-report"] = "a verification report",
    ["solver-log"] = "solver logging",
    ["output"] = "where compiled output goes",
    ["progress"] = "progress reporting",
  };

  /// <summary>A digest of the options in the resolution key of a module resolved under <paramref name="options"/>.</summary>
  public static string OptionsDigest(DafnyOptions options) {
    using var bytes = new MemoryStream();
    using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true)) {
      foreach (var option in Options(options).OrderBy(p => p.Key, StringComparer.Ordinal)) {
        writer.Write(option.Key);
        writer.Write(option.Value);
      }
    }
    return Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray()));
  }

  internal static IEnumerable<KeyValuePair<string, string>> Options(DafnyOptions options) {
    // Execution, reporting, and lookup locations are not semantic resolution inputs.
    var ignored = IgnoredOptions;
    var current = options.Options.OptionArguments.Where(p => !ignored.ContainsKey(p.Key.Name))
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
