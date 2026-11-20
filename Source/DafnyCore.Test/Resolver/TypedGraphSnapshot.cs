using System.Collections;
using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Dafny;
using Type = System.Type;

namespace DafnyCore.Test.Resolver;

// Deliberately test-only. This is an ABI-bound, detached object table, not a disk format or a
// cache admission policy. Reflection is confined to data fields; no AST getters/Clone methods run.
internal sealed record TypedGraphSnapshot(TypedGraphSnapshot.Value Root,
  ImmutableArray<TypedGraphSnapshot.Entry> Entries) {
  internal abstract record Value;
  internal sealed record Scalar(object? Data) : Value;
  internal sealed record Reference(int Id) : Value;
  internal sealed record External(string Selector) : Value;
  internal sealed record Entry(Type Type, string Kind, ImmutableArray<string> Names,
    ImmutableArray<Value> Values);

  // Only reflection metadata and immutable scalar values may survive capture. In particular,
  // Entries contain no source ASTs, delegates, collection instances, scopes or inference objects.
  public static TypedGraphSnapshot Capture(object root, GraphBindings bindings) {
    var ids = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
    var entries = new List<Entry>();
    var scopeTokens = new Dictionary<uint, int>();
    var pending = new Queue<object>();
    Value Visit(object? value) {
      if (value == null || GraphData.IsScalar(value.GetType())) { return new Scalar(value); }
      if (bindings.Selectors.TryGetValue(value, out var selector)) { return new External(selector); }
      if (ids.TryGetValue(value, out var old)) { return new Reference(old); }
      var id = entries.Count;
      ids.Add(value, id);
      entries.Add(null!); // Reserve before following cycles.
      pending.Enqueue(value);
      return new Reference(id);
    }
    var rootValue = Visit(root);
    while (pending.TryDequeue(out var value)) {
      var id = ids[value];
      var type = value.GetType();
      string kind;
      IEnumerable<string> names = [];
      IEnumerable<Value> values;
      if (value is VisibilityScope scope) {
        kind = "scope";
        values = GraphData.Tokens(scope).Select(token => {
          if (bindings.TokenSelectors.TryGetValue(token, out var name)) { return (Value)new External(name); }
          if (!scopeTokens.TryGetValue(token, out var logical)) { scopeTokens[token] = logical = scopeTokens.Count; }
          return new Scalar(logical);
        });
      } else if (value is Array array) {
        if (array.Rank != 1 || array.GetLowerBound(0) != 0) { throw new NotSupportedException("array shape"); }
        kind = "array";
        values = array.Cast<object?>().Select(Visit);
      } else if (GraphData.CollectionKind(type) is { } collection) {
        kind = collection;
        names = [GraphData.Comparer(value)];
        values = value is IDictionary dictionary
          ? dictionary.Keys.Cast<object>().SelectMany(key => new[] { Visit(key), Visit(dictionary[key]) })
          : ((IEnumerable)value).Cast<object?>().Select(Visit);
      } else {
        GraphData.RequireDataType(type);
        kind = "fields";
        var fields = GraphData.Fields(type);
        names = fields.Select(GraphData.Name);
        values = fields.Select(field => {
          try { return Visit(field.GetValue(value)); }
          catch (NotSupportedException error) { throw new NotSupportedException(GraphData.Name(field) + "/" + error.Message); }
        });
      }
      entries[id] = new(type, kind, names.ToImmutableArray(), values.ToImmutableArray());
    }
    return new(rootValue, entries.ToImmutableArray());
  }

  public object Materialize(GraphBindings bindings) {
    var objects = new object[Entries.Length];
    var tokens = new Dictionary<int, uint>();
    // Allocate ALL identities before restoring edges. Readonly back edges and mutually recursive
    // declarations must point into this table, never into the producer's graph.
    for (var i = 0; i < Entries.Length; i++) {
      var entry = Entries[i];
      objects[i] = entry.Kind switch {
        "scope" when entry.Type == typeof(VisibilityScope) => new VisibilityScope(),
        "array" when entry.Type.IsArray => Array.CreateInstance(entry.Type.GetElementType()!, entry.Values.Length),
        "list" or "dictionary" or "set" or "sorted-set" or "stack" => GraphData.NewCollection(entry.Type, entry.Names.Single()),
        "fields" => Allocate(entry),
        _ => throw new InvalidOperationException("unknown graph entry kind")
      };
    }
    var initializedValues = new bool[objects.Length];
    object ReadReference(int id) {
      if (Entries[id].Type.IsValueType && !initializedValues[id]) {
        initializedValues[id] = true;
        var fields = GraphData.Fields(Entries[id].Type);
        for (var j = 0; j < fields.Length; j++) { fields[j].SetValue(objects[id], Read(Entries[id].Values[j])); }
      }
      return objects[id];
    }
    object? Read(Value value) => value switch {
      Scalar scalar when scalar.Data == null || GraphData.IsScalar(scalar.Data.GetType()) => scalar.Data,
      Reference reference when reference.Id >= 0 && reference.Id < objects.Length => ReadReference(reference.Id),
      External external when bindings.Objects.TryGetValue(external.Selector, out var bound) => bound,
      _ => throw new InvalidOperationException("unbound graph edge")
    };
    // Hash containers must be populated after the fields used by key.GetHashCode (notably tokens).
    // Otherwise even a graph with correct edges can contain unusable dictionaries.
    foreach (var i in Enumerable.Range(0, Entries.Length).OrderBy(i => Entries[i].Kind is "dictionary" or "set" or "sorted-set" ? 1 : 0)) {
      var entry = Entries[i];
      var target = objects[i];
      switch (entry.Kind) {
        case "scope":
          foreach (var value in entry.Values) {
            uint token;
            if (value is External external && bindings.TokenObjects.TryGetValue(external.Selector, out var bound)) {
              token = bound;
            } else if (value is Scalar { Data: int logical } && logical >= 0) {
              if (!tokens.TryGetValue(logical, out token)) {
                token = GraphData.Tokens(new VisibilityScope("snapshot")).Single();
                tokens.Add(logical, token);
              }
            } else { throw new InvalidOperationException("unbound scope token"); }
            GraphData.Tokens((VisibilityScope)target).Add(token);
          }
          break;
        case "fields":
          var fields = GraphData.Fields(entry.Type);
          for (var j = 0; j < fields.Length; j++) { fields[j].SetValue(target, Read(entry.Values[j])); }
          break;
        case "array":
          for (var j = 0; j < entry.Values.Length; j++) { ((Array)target).SetValue(Read(entry.Values[j]), j); }
          break;
        case "dictionary":
          if (entry.Values.Length % 2 != 0) { throw new InvalidOperationException("dictionary arity"); }
          for (var j = 0; j < entry.Values.Length; j += 2) {
            ((IDictionary)target).Add(Read(entry.Values[j])!, Read(entry.Values[j + 1]));
          }
          break;
        default:
          var method = target.GetType().GetMethod(entry.Kind == "stack" ? "Push" : "Add")!;
          var items = entry.Kind == "stack" ? entry.Values.Reverse() : entry.Values;
          foreach (var item in items) { method.Invoke(target, [Read(item)]); }
          break;
      }
    }
    return Read(Root)!;
  }

  private static object Allocate(Entry entry) {
    GraphData.RequireDataType(entry.Type);
    var fields = GraphData.Fields(entry.Type);
    if (!entry.Names.SequenceEqual(fields.Select(GraphData.Name)) || fields.Length != entry.Values.Length) {
      throw new InvalidOperationException("typed graph field schema differs");
    }
    return RuntimeHelpers.GetUninitializedObject(entry.Type);
  }
}

// Externals are rooted in freshly resolved prerequisites and the CURRENT builtin catalogue.
// Paths are an experimental same-ABI selector, not the persistent selector contract of PR 282.
internal sealed class GraphBindings {
  public Dictionary<object, string> Selectors { get; } = new(ReferenceEqualityComparer.Instance);
  public Dictionary<string, object> Objects { get; } = new(StringComparer.Ordinal);
  public Dictionary<uint, string> TokenSelectors { get; } = new();
  public Dictionary<string, uint> TokenObjects { get; } = new(StringComparer.Ordinal);

  public static GraphBindings For(ModuleResolutionResult result, IReadOnlyList<ModuleResolutionResult> prerequisites) {
    var bindings = new GraphBindings();
    var owner = result.ResolvedDeclaration as LiteralModuleDecl;
    var modules = prerequisites.SelectMany(p => p.Signatures.Keys).ToHashSet(ReferenceEqualityComparer.Instance);
    modules.Add(result.Builtins.SystemModule);
    void Visit(object? value, string path) {
      if (value == null || GraphData.IsScalar(value.GetType()) || bindings.Selectors.ContainsKey(value)) { return; }
      if (ReferenceEquals(value, owner) || ReferenceEquals(value, owner?.ModuleDef)) { return; }
      bindings.Selectors.Add(value, path);
      bindings.Objects.Add(path, value);
      if (value is VisibilityScope scope) {
        foreach (var token in GraphData.Tokens(scope)) {
          if (bindings.TokenSelectors.ContainsKey(token)) { continue; }
          var selector = path + "/token/" + bindings.TokenSelectors.Count;
          bindings.TokenSelectors.Add(token, selector);
          bindings.TokenObjects.Add(selector, token);
        }
        return;
      }
      if (value is DafnyOptions || value is Delegate) { return; }
      if (value is ModuleDefinition module && !modules.Contains(module)) {
        Visit(module.VisibilityScope, path + "/scope");
        return;
      }
      if (value is IDictionary dictionary) {
        var i = 0;
        foreach (DictionaryEntry pair in dictionary) {
          Visit(pair.Key, path + "/key/" + i);
          Visit(pair.Value, path + "/value/" + i++);
        }
      } else if (value is IEnumerable sequence && value is not string && !value.GetType().IsValueType) {
        var i = 0;
        foreach (var item in sequence) { Visit(item, path + "/item/" + i++); }
      } else {
        // An external callback or runtime helper is a leaf, never an owned materializable node.
        if (value.GetType().Assembly != typeof(ModuleDefinition).Assembly) { return; }
        foreach (var field in GraphData.Fields(value.GetType())) { Visit(field.GetValue(value), path + "/" + GraphData.Name(field)); }
      }
    }
    Visit(result.ResolvedDeclaration.Options, "options");
    Visit(owner?.ModuleDef.EnclosingModule, "parent");
    Visit(Token.NoToken, "no-token");
    Visit(SourceOrigin.NoToken, "no-origin");
    Visit(result.Builtins.SystemModule, "system");
    // The worker's published view is pruned for translation. Resolved pretypes can still
    // reference canonical intrinsic declarations outside that view (e.g. a datatype field's
    // bitvector). Bind the catalogue too, rather than manufacturing a same-named builtin.
    Visit(result.Builtins.ResolutionCatalog, "catalog");
    foreach (var pair in result.Builtins.PreTypeBuiltins.OrderBy(p => p.Key, StringComparer.Ordinal)) {
      Visit(pair.Value, "pretype/" + pair.Key);
    }
    foreach (var declaration in result.Builtins.valuetypeDecls) { Visit(declaration, "value/" + declaration.Name); }
    foreach (var prerequisite in prerequisites.OrderBy(p => p.ResolvedDeclaration.FullDafnyName, StringComparer.Ordinal)) {
      Visit(prerequisite.ResolvedDeclaration, "prerequisite/" + prerequisite.ResolvedDeclaration.FullDafnyName);
      Visit(prerequisite.Signatures, "signatures/" + prerequisite.ResolvedDeclaration.FullDafnyName);
      Visit(prerequisite.ClassMembers, "members/" + prerequisite.ResolvedDeclaration.FullDafnyName);
    }
    return bindings;
  }
}

internal static class GraphData {
  private static readonly BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
  internal static readonly FieldInfo ScopeTokens = typeof(VisibilityScope).GetField("scopeTokens", InstanceFields)!;
  public static SortedSet<uint> Tokens(VisibilityScope scope) => (SortedSet<uint>)ScopeTokens.GetValue(scope)!;

  public static HashSet<object> OwnedObjects(object root, GraphBindings bindings) {
    var objects = new HashSet<object>(ReferenceEqualityComparer.Instance);
    var pending = new Stack<object>();
    pending.Push(root);
    while (pending.TryPop(out var value)) {
      if (IsScalar(value.GetType()) || bindings.Selectors.ContainsKey(value) || !objects.Add(value)) { continue; }
      IEnumerable<object?> children;
      if (value is VisibilityScope) { continue; }
      if (value is IDictionary dictionary) {
        children = dictionary.Keys.Cast<object>().SelectMany(key => new[] { key, dictionary[key] });
      } else if (value is Array || CollectionKind(value.GetType()) != null) {
        children = ((IEnumerable)value).Cast<object?>();
      } else { children = Fields(value.GetType()).Select(field => field.GetValue(value)); }
      foreach (var child in children) { if (child != null) { pending.Push(child); } }
    }
    return objects;
  }

  public static bool IsScalar(Type type) => type.IsPrimitive || type.IsEnum ||
    type == typeof(string) || type == typeof(decimal) || type == typeof(BigInteger) || type == typeof(Uri) || type == typeof(Guid) ||
    type == typeof(Microsoft.BaseTypes.BigDec) || type == typeof(Microsoft.BaseTypes.BigNum);

  public static FieldInfo[] Fields(Type type) {
    var fields = new List<FieldInfo>();
    for (var current = type; current != null; current = current.BaseType) {
      fields.AddRange(current.GetFields(InstanceFields));
    }
    return fields.OrderBy(Name, StringComparer.Ordinal).ToArray();
  }
  public static string Name(FieldInfo field) => field.DeclaringType!.FullName + "." + field.Name;
  public static void RequireDataType(Type type) {
    if (typeof(Delegate).IsAssignableFrom(type) ||
        (type.Assembly != typeof(ModuleDefinition).Assembly &&
         !(type.IsGenericType && type.GetGenericTypeDefinition() is var definition &&
           (definition == typeof(Tuple<,>) || definition == typeof(Tuple<,,>) || definition == typeof(ValueTuple<,>) || definition == typeof(KeyValuePair<,>) ||
            definition == typeof(ImmutableArray<>) || definition == typeof(ImmutableList<>) || definition.DeclaringType == typeof(ImmutableList<>))))) {
      throw new NotSupportedException("unsupported owned graph type: " + type.FullName);
    }
  }
  public static string? CollectionKind(Type type) {
    if (!type.IsGenericType) { return null; }
    var definition = type.GetGenericTypeDefinition();
    if (definition == typeof(List<>)) { return "list"; }
    if (definition == typeof(Dictionary<,>)) { return "dictionary"; }
    if (definition == typeof(HashSet<>)) { return "set"; }
    if (definition == typeof(SortedSet<>)) { return "sorted-set"; }
    if (definition == typeof(Stack<>)) { return "stack"; }
    return null;
  }
  public static string Comparer(object collection) {
    var type = collection.GetType();
    var property = type.GetProperty("Comparer");
    if (property == null) { return "default"; }
    var comparer = property.GetValue(collection);
    if (ReferenceEquals(comparer, StringComparer.Ordinal)) { return "ordinal"; }
    if (ReferenceEquals(comparer, ReferenceEqualityComparer.Instance)) { return "identity"; }
    var generic = CollectionKind(type) == "sorted-set" ? typeof(Comparer<>) : typeof(EqualityComparer<>);
    if (ReferenceEquals(comparer, generic.MakeGenericType(type.GetGenericArguments()[0]).GetProperty("Default")!.GetValue(null))) { return "default"; }
    throw new NotSupportedException("unsupported graph comparer: " + comparer!.GetType());
  }
  public static object NewCollection(Type type, string comparer) {
    if (CollectionKind(type) == null) { throw new InvalidOperationException("unknown graph collection"); }
    return comparer switch {
      "default" => Activator.CreateInstance(type)!,
      "ordinal" => Activator.CreateInstance(type, StringComparer.Ordinal)!,
      "identity" => Activator.CreateInstance(type, ReferenceEqualityComparer.Instance)!,
      _ => throw new InvalidOperationException("unknown graph comparer")
    };
  }
}
