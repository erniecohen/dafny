#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using DafnyCore.Verifier;
using Microsoft.Boogie;

namespace Microsoft.Dafny.Caching;

internal sealed record ProofWarningFacts(
  [property: JsonRequired] bool Vacuous, [property: JsonRequired] bool Unused,
  [property: JsonRequired] bool AssertStatement);

internal sealed record StoredProofDependency(
  [property: JsonRequired] string Kind, [property: JsonRequired] string Description,
  [property: JsonRequired] ReportPosition Start, [property: JsonRequired] ReportPosition End,
  [property: JsonRequired] bool InclusiveEnd, [property: JsonRequired] string Original,
  [property: JsonRequired] string SourceOriginal, [property: JsonRequired] ProofWarningFacts Warnings,
  [property: JsonRequired] StoredProofDependency? Related);

internal sealed record StoredProofScope(
  [property: JsonRequired] string Name, [property: JsonRequired] List<int> Dependencies);

internal sealed record StoredProofInventory(
  [property: JsonRequired] List<StoredProofDependency> Dependencies,
  [property: JsonRequired] List<StoredProofScope> Scopes);

internal sealed record StoredTaskProof(
  [property: JsonRequired] List<StoredProofDependency> External,
  [property: JsonRequired] List<int> Covered, [property: JsonRequired] bool HadScope);

/// <summary>Detached reporting facts, never a replacement for an executable proof dependency.</summary>
public sealed class ReplayedProofDependency : ProofDependency {
  internal ReplayedProofDependency(StoredProofDependency stored, TokenRange range, ReplayedProofDependency? related) {
    Kind = stored.Kind;
    Description = related == null ? stored.Description :
      $"{(stored.Kind == nameof(CallRequiresDependency) ? "requires" : "ensures")} clause at {related.RangeString()} from call";
    Range = range;
    Warnings = stored.Warnings;
    OriginalText = stored.Original;
  }
  public string Kind { get; }
  public string OriginalText { get; }
  public override string Description { get; }
  public override TokenRange Range { get; }
  internal ProofWarningFacts Warnings { get; }

  public static System.Type TypeOf(ProofDependency dependency) =>
    dependency is ReplayedProofDependency replayed ? PersistentProofReports.Kinds[replayed.Kind] : dependency.GetType();
}

/// <summary>
/// Portable dependency inventories and task membership. Identity within the potential inventory
/// is retained: equal-looking dependencies need not be the same dependency. Compound call
/// dependencies returned by GetFullIdDependency may instead be external to that inventory.
/// </summary>
internal static class PersistentProofReports {
  internal static readonly IReadOnlyDictionary<string, System.Type> Kinds = new[] {
    typeof(ProofObligationDependency), typeof(AssumedProofObligationDependency), typeof(RequiresDependency),
    typeof(EnsuresDependency), typeof(CallRequiresDependency), typeof(CallEnsuresDependency), typeof(CallDependency),
    typeof(AssumptionDependency), typeof(InvariantDependency), typeof(AssignmentDependency), typeof(FunctionDefinitionDependency)
  }.ToDictionary(t => t.Name, StringComparer.Ordinal);

  private static StoredProofDependency? Capture(SourceLocations locations, ProofDependency dependency, DafnyOptions options,
    ProofDependency? related = null) {
    var range = dependency.Range;
    var kind = dependency.GetType().Name;
    var compound = kind is nameof(CallRequiresDependency) or nameof(CallEnsuresDependency);
    var description = compound ? "" : dependency.Description;
    var storedRelated = related == null ? null : Capture(locations, related, options);
    if (compound && storedRelated == null || !compound && related != null ||
        !Kinds.TryGetValue(kind, out var type) || dependency.GetType() != type || range == null || !locations.PortableReportText(description) ||
        locations.ProofPosition(range.StartToken) is not { } start ||
        locations.ProofPosition(range.EndToken) is not { } end ||
        locations.ProofToken(start) is not { } sourceStart || locations.ProofToken(end) is not { } sourceEnd ||
        range.InclusiveEnd && range.EndToken.val.Length != sourceEnd.val.Length) { return null; }
    return new StoredProofDependency(kind, description, start, end, range.InclusiveEnd,
      range.PrintOriginal(), new TokenRange(sourceStart, range.InclusiveEnd ? sourceEnd : null).PrintOriginal(),
      ProofDependencyWarnings.CacheWarningFacts(dependency, options), storedRelated);
  }

  private static ReplayedProofDependency? Restore(SourceLocations locations, StoredProofDependency stored) {
    if (stored?.Kind == null || !Kinds.ContainsKey(stored.Kind) || stored.Description == null || stored.Original == null ||
        stored.SourceOriginal == null || stored.Warnings == null || locations.ProofToken(stored.Start) is not { } start ||
        locations.ProofToken(stored.End) is not { } end || start.Uri != end.Uri || end.pos < start.pos ||
        (!stored.InclusiveEnd && start != end)) { return null; }
    if (stored.Warnings.Vacuous && stored.Kind is not (nameof(ProofObligationDependency) or nameof(EnsuresDependency)) ||
        stored.Warnings.Unused && stored.Kind is not (nameof(RequiresDependency) or nameof(AssumptionDependency)) ||
        stored.Warnings.AssertStatement && stored.Kind != nameof(ProofObligationDependency)) { return null; }
    var expectedRelated = stored.Kind switch {
      nameof(CallRequiresDependency) => nameof(RequiresDependency),
      nameof(CallEnsuresDependency) => nameof(EnsuresDependency),
      _ => null
    };
    ReplayedProofDependency? related = null;
    if (expectedRelated != null) {
      if (stored.Description != "" || stored.Related?.Kind != expectedRelated || stored.Related.Related != null ||
          Restore(locations, stored.Related) is not { } restored) { return null; }
      related = restored;
    } else if (stored.Related != null) { return null; }
    var range = new TokenRange(start, stored.InclusiveEnd ? end : null);
    return range.PrintOriginal() == stored.SourceOriginal ? new ReplayedProofDependency(stored, range, related) : null;
  }

  internal sealed class Recorder {
    private readonly SourceLocations locations;
    private readonly DafnyOptions options;
    private readonly ProofDependencyManager manager;
    private readonly Dictionary<ProofDependency, int> indices = new(ReferenceEqualityComparer.Instance);
    public StoredProofInventory? Inventory { get; }

    public Recorder(Program program, ProofDependencyManager manager) {
      locations = SourceLocations.For(program);
      options = program.Options;
      this.manager = manager;
      if (!options.TrackVerificationCoverage) { return; }
      try {
        var dependencies = new List<StoredProofDependency>();
        var scopes = new List<StoredProofScope>();
        lock (manager.SyncRoot) {
          foreach (var (name, (_, potential)) in manager.idsByMemberName) {
            var members = new List<int>();
            foreach (var dependency in potential) {
              if (!indices.TryGetValue(dependency, out var index)) {
                if (Capture(locations, dependency, options) is not { } stored) { return; }
                index = dependencies.Count;
                indices.Add(dependency, index);
                dependencies.Add(stored);
              }
              members.Add(index);
            }
            scopes.Add(new StoredProofScope(name, members));
          }
        }
        Inventory = new StoredProofInventory(dependencies, scopes);
      } catch (Exception error) when (error is not OutOfMemoryException) { }
    }

    public StoredTaskProof? CaptureTask(IEnumerable<TrackedNodeComponent> components, string scope) {
      if (Inventory == null) { return null; }
      try {
        var external = new List<StoredProofDependency>();
        var covered = new List<int>();
        var local = new Dictionary<ProofDependency, int>(ReferenceEqualityComparer.Instance);
        lock (manager.SyncRoot) {
          foreach (var component in components) {
            var dependency = manager.GetFullIdDependency(component);
            if (!indices.TryGetValue(dependency, out var index) && !local.TryGetValue(dependency, out index)) {
              var related = component switch {
                TrackedCallRequiresGoal c => manager.ProofDependenciesById[c.requiresId],
                TrackedCallRequiresAssumed c => manager.ProofDependenciesById[c.requiresId],
                TrackedCallEnsures c => manager.ProofDependenciesById[c.ensuresId],
                _ => null
              };
              if (Capture(locations, dependency, options, related) is not { } stored) { return null; }
              index = Inventory.Dependencies.Count + external.Count;
              local.Add(dependency, index);
              external.Add(stored);
            }
            covered.Add(index);
          }
        }
        return new StoredTaskProof(external, covered, Inventory.Scopes.Any(s => s.Name == scope));
      } catch (Exception error) when (error is not OutOfMemoryException) { return null; }
    }
  }

  internal sealed class Restorer {
    private readonly SourceLocations locations;
    private readonly int count;
    public ProofDependencyManager Manager { get; } = new();

    private Restorer(SourceLocations locations, int count) { this.locations = locations; this.count = count; }

    public static Restorer? Create(SourceLocations locations, StoredProofInventory? inventory) => Create(locations, inventory, out _);

    public static Restorer? Create(SourceLocations locations, StoredProofInventory? inventory, out string reason) {
      reason = "a stored module has no current proof inventory";
      if (inventory?.Dependencies == null || inventory.Scopes == null) { return null; }
      var result = new Restorer(locations, inventory.Dependencies.Count);
      for (var i = 0; i < inventory.Dependencies.Count; i++) {
        if (Restore(locations, inventory.Dependencies[i]) is not { } dependency) {
          reason = $"a stored proof dependency has no current range or kind (index {i})";
          return null;
        }
        result.Manager.ProofDependenciesById.Add(Id(i), dependency);
      }
      foreach (var scope in inventory.Scopes) {
        if (scope?.Name == null || scope.Dependencies == null ||
            result.Manager.idsByMemberName.ContainsKey(scope.Name)) {
          reason = "a stored proof scope has no unique name";
          return null;
        }
        var potential = new HashSet<ProofDependency>();
        foreach (var index in scope.Dependencies) {
          if (index < 0 || index >= result.count || !potential.Add(result.Manager.ProofDependenciesById[Id(index)])) { return null; }
        }
        // Decl is used only by refactoring/analysis, which ReplayBypass excludes. Reporting
        // consumes the scope name and potential set; task declarations bind separately.
        result.Manager.idsByMemberName.Add(scope.Name, (null!, potential));
      }
      return result;
    }

    private static string Id(int index) => "replayed-dependency-" + index;

    public IReadOnlyList<TrackedNodeComponent>? Task(StoredTaskProof? stored, int task, string scope) {
      if (stored?.External == null || stored.Covered == null || stored.HadScope != Manager.idsByMemberName.ContainsKey(scope)) { return null; }
      string Key(int index) => index < count ? Id(index) : $"replayed-task-{task}-{index - count}";
      for (var i = 0; i < stored.External.Count; i++) {
        if (Restore(locations, stored.External[i]) is not { } dependency) { return null; }
        Manager.ProofDependenciesById.Add(Key(count + i), dependency);
      }
      var covered = new List<TrackedNodeComponent>();
      foreach (var index in stored.Covered) {
        if (index < 0 || index >= (long)count + stored.External.Count) { return null; }
        covered.Add(new LabeledNodeComponent(Key(index)));
      }
      return covered;
    }
  }
}
