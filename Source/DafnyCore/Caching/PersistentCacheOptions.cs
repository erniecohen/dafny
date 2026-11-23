#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Dafny.Caching;

/// <summary>
/// The options each persistent table of --persistent-translation keys, by stage, as digests of
/// those options alone. Keys are default-deny: every option of the option bag is keyed unless it is
/// on the list of options that change no result (<see cref="ModuleResolutionKey"/>), and every
/// public scalar or string-sequence member of the effective options is keyed as well. The
/// classification in docs/dev/design/261-option-classes.tsv is checked against these digests.
/// </summary>
public static class PersistentCacheOptions {
  /// <summary>The resolution, translation and results digests of the options alone.</summary>
  public static IReadOnlyDictionary<string, string> StageDigests(DafnyOptions options) {
    var resolution = ResolutionParts(options);
    var translation = new List<string>(resolution);
    ModuleTranslationCache.AddOptionParts(translation, options, true);
    // The translator flags the translation key adds (TranslatorFlags.ReportRanges).
    translation.Add("report-ranges"); translation.Add(options.Get(global::DafnyCore.Snippets.ShowSnippets).ToString());
    return new SortedDictionary<string, string>(StringComparer.Ordinal) {
      ["resolution"] = Digest("resolution", resolution),
      ["translation"] = Digest("translation", translation),
      // A module's results are keyed by its graph and its environment; preparation is not
      // persisted, and a graph hit skips it, so preparation is keyed by the same environment. The
      // environment also includes the task-result cache's frozen solver options, which this digest
      // leaves out: they are fixed only once the solver is set up.
      ["results"] = Digest("results", EnvironmentParts(options)),
    };
  }

  private static List<string> ResolutionParts(DafnyOptions options) =>
    ModuleResolutionKey.Options(options).OrderBy(p => p.Key, StringComparer.Ordinal)
      .SelectMany(p => new[] { p.Key, p.Value }).ToList();

  /// <summary>The option parts of a module-results environment, less the solver's own identity.</summary>
  internal static List<string> EnvironmentParts(DafnyOptions options) {
    var parts = new List<string>();
    ModuleTranslationCache.AddOptionParts(parts, options, true);
    foreach (var (name, value) in ModuleResolutionKey.Options(options).OrderBy(p => p.Key, StringComparer.Ordinal)) {
      if (name is "persistent-translation" or "persistent-translation-audit") { continue; }
      parts.Add("option:" + name); parts.Add(value);
    }
    return parts;
  }

  private static string Digest(string stage, List<string> parts) =>
    CacheDigest.Compute("persistent-options-" + stage, parts.Select(p => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(p)).ToArray());
}
