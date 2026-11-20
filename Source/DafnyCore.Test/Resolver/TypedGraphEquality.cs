using System.Collections;
using Microsoft.Dafny;

namespace DafnyCore.Test.Resolver;

// Walks the LIVE expected and actual graphs, independently of the frozen table and materializer.
// Two-way maps check aliasing as well as cycles: duplicating a shared node, merging distinct nodes
// and redirecting an edge to an equal-looking declaration are all observable differences.
internal sealed class TypedGraphEquality(GraphBindings bindings) {
  private readonly Dictionary<object, object> forward = new(ReferenceEqualityComparer.Instance);
  private readonly Dictionary<object, object> reverse = new(ReferenceEqualityComparer.Instance);
  private readonly List<(VisibilityScope Expected, VisibilityScope Actual)> scopes = new();

  private readonly Queue<(object? Expected, object? Actual, string Path)> pending = new();

  public string? Difference(object? expected, object? actual) {
    forward.Clear();
    reverse.Clear();
    scopes.Clear();
    pending.Clear();
    pending.Enqueue((expected, actual, "root"));
    while (pending.TryDequeue(out var pair)) {
      var difference = Compare(pair.Expected, pair.Actual, pair.Path);
      if (difference != null) { return difference; }
    }
    // A token's incidence in ALL paired scopes identifies its logical role. Comparing these
    // multisets avoids depending on allocation order or greedily pairing symmetric tokens.
    string[] Incidence(bool actual) {
      var occurrences = new Dictionary<uint, List<int>>();
      for (var i = 0; i < scopes.Count; i++) {
        foreach (var token in GraphData.Tokens(actual ? scopes[i].Actual : scopes[i].Expected)) {
          if (!occurrences.TryGetValue(token, out var uses)) { occurrences[token] = uses = []; }
          uses.Add(i);
        }
      }
      return occurrences.Select(pair => (bindings.TokenSelectors.TryGetValue(pair.Key, out var selector)
        ? selector : "owned") + ":" + string.Join(",", pair.Value)).Order(StringComparer.Ordinal).ToArray();
    }
    return Incidence(false).SequenceEqual(Incidence(true)) ? null : "root: scope token incidence";
  }

  private string? Compare(object? expected, object? actual, string path) {
    string? Fail(string reason) => path + ": " + reason;
    if (expected == null || actual == null) { return expected == actual ? null : Fail("null edge"); }
    var type = expected.GetType();
    if (type != actual.GetType()) { return Fail("type " + type.Name + " != " + actual.GetType().Name); }
    if (GraphData.IsScalar(type)) { return Equals(expected, actual) ? null : Fail("scalar value"); }
    if (bindings.Selectors.ContainsKey(expected)) { return ReferenceEquals(expected, actual) ? null : Fail("external identity"); }
    if (forward.TryGetValue(expected, out var previous)) { return ReferenceEquals(previous, actual) ? null : Fail("split identity"); }
    if (reverse.ContainsKey(actual)) { return Fail("merged identity"); }
    forward.Add(expected, actual);
    reverse.Add(actual, expected);
    if (expected is VisibilityScope scope) {
      scopes.Add((scope, (VisibilityScope)actual));
      return null;
    }
    if (GraphData.CollectionKind(type) == "dictionary") {
      var dictionary = (IDictionary)expected;
      var other = (IDictionary)actual;
      if (dictionary.Count != other.Count) { return Fail("dictionary count"); }
      if (GraphData.Comparer(expected) != GraphData.Comparer(actual)) { return Fail("dictionary comparer"); }
      var a = dictionary.GetEnumerator();
      var b = other.GetEnumerator();
      var i = 0;
      while (a.MoveNext() && b.MoveNext()) {
        pending.Enqueue((a.Key, b.Key, path + "/key/" + i));
        pending.Enqueue((a.Value, b.Value, path + "/value/" + i++));
      }
      return null;
    }
    if (expected is Array || GraphData.CollectionKind(type) != null) {
      var sequence = (IEnumerable)expected;
      if (GraphData.CollectionKind(type) != null && GraphData.Comparer(expected) != GraphData.Comparer(actual)) {
        return Fail("sequence comparer");
      }
      var a = sequence.GetEnumerator();
      var b = ((IEnumerable)actual).GetEnumerator();
      var i = 0;
      while (true) {
        var nextA = a.MoveNext();
        var nextB = b.MoveNext();
        if (nextA != nextB) { return Fail("sequence count"); }
        if (!nextA) { return null; }
        pending.Enqueue((a.Current, b.Current, path + "/item/" + i++));
      }
    }
    GraphData.RequireDataType(type);
    foreach (var field in GraphData.Fields(type)) {
      // Parse-cache lineage and TypeProxy's diagnostic-only counter are not semantic identities.
      // The two-way maps still check proxy identity and all its target/constraint edges.
      if (field.DeclaringType == typeof(ModuleDecl) && field.Name == "<CloneId>k__BackingField") { continue; }
      if (field.DeclaringType == typeof(TypeProxy) && field.Name == "id") { continue; }
      pending.Enqueue((field.GetValue(expected), field.GetValue(actual), path + "/" + field.Name));
    }
    return null;
  }
}
