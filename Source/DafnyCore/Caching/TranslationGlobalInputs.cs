#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Microsoft.Dafny;

/// <summary>
/// Current modular-termination answers, frozen before a translation key is used. Local resolution
/// identities remain acyclic; only the translation's finalized identity includes this projection.
/// </summary>
internal sealed class TranslationGlobalInputs {
  internal readonly record struct Facts(bool Dispatch, bool ArgumentsOnly, bool ReachesDispatch);
  private static readonly ConditionalWeakTable<Program, ConcurrentDictionary<ModuleDefinition, TranslationGlobalInputs>> Views = new();
  private readonly Dictionary<(MemberDecl, ModuleDefinition?), Facts> answers = new();
  private readonly ModularTerminationAnalysis analysis;
  private int incomplete;
  public bool Complete => Volatile.Read(ref incomplete) == 0;
  public string Digest { get; }
  public string FinalIdentity { get; }

  public static TranslationGlobalInputs? For(Program program, ModuleDefinition module) =>
    program.Options.Get(CommonOptionBag.TranslationGlobalInputs) && program.Options.Get(CommonOptionBag.TypeSystemRefresh) &&
    module.ResolutionKey != null && module.TranslationInputs != null &&
    program.Options.AdditionalPlugins.Count == 0 && program.Options.AdditionalPluginArguments.Count == 0 &&
    !program.Options.Get(RunAllTestsMainMethod.IncludeTestRunner) && ModularTermination.Active(program.Options, program)
      ? Views.GetOrCreateValue(program).GetOrAdd(module, m => new TranslationGlobalInputs(program, m)) : null;

  private TranslationGlobalInputs(Program program, ModuleDefinition module) {
    analysis = ModularTerminationAnalysis.Of(program);
    var saved = Type.SaveScopes();
    Type.ResetScopes();
    try {
      // Include negative answers and every potential member of every resolved import/export view.
      // A translation may emit imported declarations as well as its own, so retain each caller view.
      var modules = program.RawModules().Where(m => ReferenceEquals(m, module) ||
          module.TranslationInputs!.ModuleNames.Contains(m.FullName))
        .Append(program.SystemModuleManager.SystemModule).Distinct().ToArray();
      var members = modules.SelectMany(m => m.TopLevelDecls).OfType<TopLevelDeclWithMembers>()
        .Where(t => !ReferenceEquals(t.EnclosingModuleDefinition, program.SystemModuleManager.SystemModule) ||
          module.TranslationInputs!.BuiltinNames.Contains(t.Name))
        .SelectMany(t => t.Members.Concat(t.InheritedMembers))
        .SelectMany(m => m switch {
          ExtremePredicate { PrefixPredicate: { } prefix } => new MemberDecl[] { m, prefix },
          ExtremeLemma { PrefixLemma: { } prefix } => new MemberDecl[] { m, prefix },
          _ => new[] { m }
        }).Distinct().ToArray();
      var rows = new SortedDictionary<string, Facts>(StringComparer.Ordinal);
      foreach (var member in members) {
        foreach (var caller in modules.Cast<ModuleDefinition?>().Prepend(null)) {
          var facts = Current(member, caller);
          answers.Add((member, caller), facts);
          // FullDafnyName names the resolved view, not a source path or generated global ordinal.
          var name = Frame(member.GetType().Name, member.FullDafnyName,
            caller == null ? "none" : "module:" + caller.FullName);
          if (rows.TryGetValue(name, out var previous) && previous != facts) {
            Interlocked.Exchange(ref incomplete, 1); // Ambiguous nominal view: never publish it.
          }
          rows[name] = facts;
        }
      }
      Digest = Hash(writer => {
        writer.Write("dafny/translation-global-inputs/v1");
        writer.Write(rows.Count);
        foreach (var (name, facts) in rows) {
          writer.Write(name);
          writer.Write(facts.Dispatch);
          writer.Write(facts.ArgumentsOnly);
          writer.Write(facts.ReachesDispatch);
        }
      });
      FinalIdentity = Hash(writer => {
        writer.Write("dafny/final-translation-resolution/v1");
        writer.Write(module.ResolutionIdentity ?? module.ResolutionKey ?? "");
        writer.Write(Digest);
      });
    } finally {
      Type.RestoreScopes(saved);
    }
  }

  public Facts Read(MemberDecl member, ModuleDefinition? caller) {
    if (answers.TryGetValue((member, caller), out var facts)) { return facts; }
    // A newly introduced translator read must not silently become false or authorize a snapshot.
    // Finish the current native translation, but make both cache publishers reject its key.
    Interlocked.Exchange(ref incomplete, 1);
    return Current(member, caller);
  }

  internal Facts Current(MemberDecl member, ModuleDefinition? caller) => new(
    ModularTermination.IsOpenDispatch(member, analysis.OpenTraits), analysis.AppliesOnlyItsArguments(member),
    analysis.ReachesOpenDispatch(member, caller));

  private static string Frame(params string[] parts) =>
    string.Concat(parts.Select(p => p.Length.ToString(CultureInfo.InvariantCulture) + ":" + p));
  private static string Hash(Action<BinaryWriter> write) {
    using var bytes = new MemoryStream();
    using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true)) { write(writer); }
    return Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray()));
  }
}
