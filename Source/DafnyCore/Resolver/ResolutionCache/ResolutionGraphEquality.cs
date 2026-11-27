#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// Compares two live resolution graphs (issue 261), independently of the snapshot encoding: the graph
/// of a module's fresh resolution and the graph that its cache entry materialized. Two-way maps pair the
/// objects of the graphs, so a duplicated shared object, two merged objects and an edge redirected to an
/// equal-looking object are all differences. Statics and the runtime's shared empty values must be the
/// same objects, and so must the boundary objects that both graphs share (the parsed context of the
/// module and the objects of its prerequisites), which are not entered. Objects outside the snapshot schema (the
/// options, the reporter, the program) are paired but not entered. Visibility-scope tokens are compared by their
/// incidence in the paired scopes, Guids and quantifier allocation identities up to a renaming,
/// and print labels not at all. An object
/// in both graphs is shared by them and is not entered.
/// </summary>
internal sealed class ResolutionGraphEquality(Func<object, bool> boundary) {
  private readonly Dictionary<object, object> forward = new(ReferenceEqualityComparer.Instance);
  private readonly Dictionary<object, object> reverse = new(ReferenceEqualityComparer.Instance);
  private readonly List<(VisibilityScope Expected, VisibilityScope Actual)> scopes = [];
  private readonly Queue<(object? Expected, object? Actual, GraphPath Path)> pending = new();
  private readonly Dictionary<object, object> pairs = new(ReferenceEqualityComparer.Instance);
  private readonly Dictionary<object, object> names = new();
  private readonly Dictionary<object, object> reverseNames = new();

  // Keep one edge of a diagnostic path, not a copy of its entire ancestry for every queued field.
  // Large expanded match trees need millions of comparisons even when the graphs are equal.
  private sealed record GraphPath(GraphPath? Parent, string Segment) {
    public override string ToString() {
      var parts = new Stack<string>();
      for (GraphPath? path = this; path != null; path = path.Parent) { parts.Push(path.Segment); }
      return string.Join("/", parts);
    }
  }

  /// <summary>
  /// Counters that only label an object when it is printed, and are not unique (each pre-type resolver counts
  /// from zero). A fresh resolution may draw other values; the objects themselves are paired by identity.
  /// </summary>
  private static readonly HashSet<System.Reflection.FieldInfo> NamingFields = [
    typeof(PreTypeProxy).GetField(nameof(PreTypeProxy.UniqueId))!,
    typeof(TypeRefinementWrapper).GetField(nameof(TypeRefinementWrapper.UniqueId))!,
  ];

  // Unlike the non-unique print labels above, quantifier counters identify distinct allocations.
  // Preserve their equality pattern with a bijection rather than dropping the field or comparing
  // the process-wide counter values. The field tags the naming domain separately from Guids.
  private static readonly System.Reflection.FieldInfo QuantifierIdentity = typeof(QuantifierExpr)
    .GetField("UniqueId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

  /// <summary>Pair two boundary objects that stand for each other, such as a parsed declaration and its reused result.</summary>
  public void Pair(object expected, object actual) => pairs[expected] = actual;

  /// <summary>The objects compared field by field, collection by collection or scope by scope.</summary>
  public int Compared { get; private set; }

  /// <summary>The objects paired without being entered, because the snapshot schema does not hold them.</summary>
  public int Opaque { get; private set; }

  /// <summary>The first difference between the graphs, as a path from the root, or null if they are equal.</summary>
  public string? Difference(object? expected, object? actual) {
    pending.Enqueue((expected, actual, new GraphPath(null, "root")));
    while (pending.TryDequeue(out var pair)) {
      if (Compare(pair.Expected, pair.Actual, pair.Path) is { } difference) {
        pending.Clear();
        return difference;
      }
    }
    // A token's incidence in all paired scopes identifies its role; comparing these multisets does not
    // depend on allocation order.
    string[] Incidence(bool actual) {
      var occurrences = new Dictionary<uint, List<int>>();
      for (var i = 0; i < scopes.Count; i++) {
        foreach (var token in ResolutionGraphSchema.Tokens(actual ? scopes[i].Actual : scopes[i].Expected)) {
          if (!occurrences.TryGetValue(token, out var uses)) { occurrences[token] = uses = []; }
          uses.Add(i);
        }
      }
      return occurrences.Values.Select(uses => string.Join(",", uses)).Order(StringComparer.Ordinal).ToArray();
    }
    return Incidence(false).SequenceEqual(Incidence(true)) ? null : "root: scope token incidence";
  }

  private string? Compare(object? expected, object? actual, GraphPath path) {
    string Fail(string reason) => path.ToString() + ": " + reason;
    if (expected == null || actual == null) { return expected == actual ? null : Fail("null edge"); }
    var type = expected.GetType();
    if (type != actual.GetType()) { return Fail("type " + type.Name + " != " + actual.GetType().Name); }
    if (expected is Guid) {
      // A Guid names an object (a declaration's clone identity), and a fresh resolution draws new ones.
      return Rename(expected, actual, Fail);
    }
    if (ResolutionGraphSchema.IsScalar(type) || type.IsEnum) { return Equals(expected, actual) ? null : Fail("value " + expected + " != " + actual); }
    if (ResolutionGraphSchema.IsValueRecord(type)) {
      // An immutable record of scalars is a value: a clone shares its original's, and which copy a module holds
      // does not matter.
      return Equals(expected, actual) ? null : Fail("value " + expected + " != " + actual);
    }
    if (pairs.TryGetValue(expected, out var paired)) { return ReferenceEquals(paired, actual) ? null : Fail("paired identity"); }
    if (ResolutionGraphSchema.Statics.ContainsKey(expected) || ResolutionGraphSchema.SingletonName(expected) != null ||
        boundary(expected) || boundary(actual)) {
      return ReferenceEquals(expected, actual) ? null : Fail("shared identity");
    }
    if (!type.IsValueType) {
      if (forward.TryGetValue(expected, out var previous)) { return ReferenceEquals(previous, actual) ? null : Fail("split identity"); }
      if (reverse.TryGetValue(actual, out var other)) { return ReferenceEquals(other, expected) ? null : Fail("merged identity"); }
      forward.Add(expected, actual);
      reverse.Add(actual, expected);
      // The graphs are built apart, so an object in both is shared by them (a builtin of the initial view), and
      // its mutable contents are those of both: what the module adds to it is compared as its effects.
      if (ReferenceEquals(expected, actual)) { return null; }
    }
    if (expected is VisibilityScope scope) {
      Compared++;
      scopes.Add((scope, (VisibilityScope)actual));
      return ResolutionGraphSchema.Ids(scope).SetEquals(ResolutionGraphSchema.Ids((VisibilityScope)actual)) ? null : Fail("scope names");
    }
    if (type == typeof(object)) { return null; }
    if (ResolutionGraphSchema.CollectionKind(type) is { } kind) {
      Compared++;
      if (ResolutionGraphSchema.Comparer(expected, out var comparerType) != ResolutionGraphSchema.Comparer(actual, out var otherType) ||
          comparerType != otherType) {
        return Fail("comparer");
      }
      if (expected is IDictionary dictionary) {
        var otherDictionary = (IDictionary)actual;
        if (dictionary.Count != otherDictionary.Count) {
          static string Keys(IDictionary d) => string.Join(",", d.Keys.Cast<object>().Select(k => k is Declaration n ? n.Name : k.ToString()).Order(StringComparer.Ordinal));
          return Fail("count " + dictionary.Count + " != " + otherDictionary.Count + " (" + Keys(dictionary) + " / " + Keys(otherDictionary) + ")");
        }
        var a = dictionary.GetEnumerator();
        var b = otherDictionary.GetEnumerator();
        for (var i = 0; a.MoveNext() && b.MoveNext(); i++) {
          pending.Enqueue((a.Key, b.Key, new GraphPath(path, "key" + i)));
          pending.Enqueue((a.Value, b.Value, new GraphPath(path, "value" + i)));
        }
        return null;
      }
      return Sequence((IEnumerable)expected, (IEnumerable)actual, path, Fail);
    }
    if (expected is Array array) {
      Compared++;
      return Sequence(array, (Array)actual, path, Fail);
    }
    try {
      ResolutionGraphSchema.RequireDataType(type);
    } catch (ResolutionSnapshotUnsupported) {
      Opaque++;
      return null;
    }
    Compared++;
    foreach (var field in ResolutionGraphSchema.Fields(type)) {
      if (NamingFields.Contains(field)) { continue; }
      var fieldPath = new GraphPath(path, field.Name);
      var expectedValue = field.GetValue(expected);
      var actualValue = field.GetValue(actual);
      if (field == QuantifierIdentity) {
        if (Rename((field, expectedValue), (field, actualValue), reason => fieldPath + ": " + reason) is { } difference) {
          return difference;
        }
      } else {
        pending.Enqueue((expectedValue, actualValue, fieldPath));
      }
    }
    return null;
  }

  /// <summary>Names agree if one renaming maps the names of the one graph onto those of the other.</summary>
  private string? Rename(object expected, object actual, Func<string, string> fail) {
    if (names.TryGetValue(expected, out var mapped)) { return Equals(mapped, actual) ? null : fail("split name " + expected); }
    if (reverseNames.TryGetValue(actual, out var inverse)) { return Equals(inverse, expected) ? null : fail("merged name " + actual); }
    names.Add(expected, actual);
    reverseNames.Add(actual, expected);
    return null;
  }

  private string? Sequence(IEnumerable expected, IEnumerable actual, GraphPath path, Func<string, string> fail) {
    var a = expected.GetEnumerator();
    var b = actual.GetEnumerator();
    for (var i = 0; ; i++) {
      var nextA = a.MoveNext();
      if (nextA != b.MoveNext()) { return fail("length"); }
      if (!nextA) { return null; }
      pending.Enqueue((a.Current, b.Current, new GraphPath(path, i.ToString())));
    }
  }
}
