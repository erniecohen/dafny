#nullable enable
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Microsoft.Dafny;

/// <summary>A graph that the persistent resolution cache does not support. The module is resolved, not cached.</summary>
internal sealed class ResolutionSnapshotUnsupported(string reason) : Exception(reason);

/// <summary>A snapshot that does not match this producer or this program. The module is resolved afresh.</summary>
internal sealed class ResolutionSnapshotInvalid(string reason) : Exception(reason);

/// <summary>
/// The runtime shapes that a resolution snapshot may hold (issue 261). A snapshot stores the data
/// fields of objects of DafnyCore types, of a closed set of collection and value types, and of
/// scalars. Anything else (delegates, lazy values, tasks, foreign objects) makes the module
/// uncacheable.
/// </summary>
internal static class ResolutionGraphSchema {
  private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
  private static readonly ConcurrentDictionary<System.Type, FieldInfo[]> FieldCache = new();
  internal static readonly Assembly Core = typeof(ModuleDefinition).Assembly;
  private static readonly Assembly BaseTypes = typeof(Microsoft.BaseTypes.BigDec).Assembly;
  internal static readonly FieldInfo ScopeTokens = typeof(VisibilityScope).GetField("scopeTokens", InstanceFields)!;
  private static readonly FieldInfo ScopeIds = typeof(VisibilityScope).GetField("scopeIds", InstanceFields)!;

  /// <summary>Fields that depend on where the program was parsed, not on what it says.</summary>
  private static readonly HashSet<string> ContextualFields = new(StringComparer.Ordinal) {
    typeof(ModuleDecl).FullName + ".<CloneId>k__BackingField",
    typeof(ModuleDecl).FullName + ".<Height>k__BackingField",
    typeof(ModuleDecl).FullName + ".Height",
    typeof(ModuleDefinition).FullName + ".Height",
    typeof(ModuleDefinition).FullName + ".<Height>k__BackingField",
    typeof(ModuleDefinition).FullName + ".<ResolutionKey>k__BackingField",
    typeof(ModuleDefinition).FullName + ".<ResolutionIdentity>k__BackingField",
    // The include directives of the parsed files, with their files' options.
    typeof(DefaultModuleDefinition).FullName + ".<Includes>k__BackingField",
    // The formatter's copies of the parsed declarations, taken before resolution.
    typeof(ModuleDefinition).FullName + ".preResolveTopLevelDecls",
    typeof(ModuleDefinition).FullName + ".preResolvePrefixNamedModules",
  };

  /// <summary>The source coordinates of a token. The structure digest leaves them out.</summary>
  private static readonly HashSet<string> PositionalFields = new(StringComparer.Ordinal) {
    typeof(Token).FullName + ".<pos>k__BackingField",
    typeof(Token).FullName + ".<line>k__BackingField",
    typeof(Token).FullName + ".<col>k__BackingField",
  };

  public static SortedSet<uint> Tokens(VisibilityScope scope) => (SortedSet<uint>)ScopeTokens.GetValue(scope)!;
  public static SortedSet<string> Ids(VisibilityScope scope) => (SortedSet<string>)ScopeIds.GetValue(scope)!;

  public static bool IsScalar(System.Type type) => ScalarCache.GetOrAdd(type, static t => t.IsPrimitive || t.IsEnum ||
    t == typeof(string) || t == typeof(decimal) || t == typeof(BigInteger) || t == typeof(Uri) || t == typeof(Guid));
  private static readonly ConcurrentDictionary<System.Type, bool> ScalarCache = new();

  /// <summary>Immutable records whose fields are all scalars, compared by value rather than identity.</summary>
  public static bool IsValueRecord(System.Type type) => type == typeof(TerminationSourceContract);

  public static FieldInfo[] Fields(System.Type type) => FieldCache.GetOrAdd(type, t => {
    var fields = new List<FieldInfo>();
    for (var current = t; current != null; current = current.BaseType) {
      fields.AddRange(current.GetFields(InstanceFields));
    }
    return fields.OrderBy(Name, StringComparer.Ordinal).ToArray();
  });

  private static readonly ConcurrentDictionary<FieldInfo, string> NameCache = new();
  public static string Name(FieldInfo field) => NameCache.GetOrAdd(field, f => f.DeclaringType!.FullName + "." + f.Name);
  public static bool IsContextual(FieldInfo field) => ContextualFields.Contains(Name(field));
  public static bool IsPositional(FieldInfo field) => PositionalFields.Contains(Name(field));

  /// <summary>
  /// Write a snapshot's fields without reflection on every object. The schema is still checked against
  /// every entry before these writers are used. Unbox value-type targets by address so a write updates
  /// the original box, including private and readonly instance fields, just as FieldInfo.SetValue does.
  /// </summary>
  public static Action<object, object?>[] FieldWriters(System.Type type) => FieldWriterCache.GetOrAdd(type,
    static type => Fields(type).Select(FieldWriter).ToArray());

  private static readonly ConcurrentDictionary<System.Type, Action<object, object?>[]> FieldWriterCache = new();

  private static Action<object, object?> FieldWriter(FieldInfo field) {
    // Keep the existing behavior on runtimes that cannot generate code.
    if (!RuntimeFeature.IsDynamicCodeSupported) { return field.SetValue; }
    var method = new DynamicMethod("RestoreResolutionField", typeof(void), [typeof(object), typeof(object)],
      typeof(ResolutionGraphSchema).Module, true);
    var il = method.GetILGenerator();
    il.Emit(OpCodes.Ldarg_0);
    il.Emit(field.DeclaringType!.IsValueType ? OpCodes.Unbox : OpCodes.Castclass, field.DeclaringType);
    il.Emit(OpCodes.Ldarg_1);
    il.Emit(field.FieldType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, field.FieldType);
    il.Emit(OpCodes.Stfld, field);
    il.Emit(OpCodes.Ret);
    return method.CreateDelegate<Action<object, object?>>();
  }

  /// <summary>Throws when <paramref name="type"/> is not data a snapshot may hold; the verdict is memoized per type.</summary>
  public static void RequireDataType(System.Type type) {
    var verdict = DataTypeVerdicts.GetOrAdd(type, static t => {
      try { CheckDataType(t); return null; } catch (ResolutionSnapshotUnsupported e) { return e.Message; }
    });
    if (verdict != null) { throw new ResolutionSnapshotUnsupported(verdict); }
  }
  private static readonly ConcurrentDictionary<System.Type, string?> DataTypeVerdicts = new();

  private static void CheckDataType(System.Type type) {
    if (typeof(Delegate).IsAssignableFrom(type) || type.IsPointer || type.IsByRef) {
      throw new ResolutionSnapshotUnsupported("unsupported type " + type.FullName);
    }
    if (type.Assembly == Core && !type.ContainsGenericParameters) {
      if (type.Namespace?.StartsWith("Microsoft.Dafny.Compilers", StringComparison.Ordinal) == true ||
          typeof(ErrorReporter).IsAssignableFrom(type) || typeof(DafnyOptions).IsAssignableFrom(type) ||
          typeof(Program).IsAssignableFrom(type) || typeof(CompilationData).IsAssignableFrom(type) ||
          typeof(ProgramResolver).IsAssignableFrom(type) || typeof(ModuleResolver).IsAssignableFrom(type) ||
          typeof(SystemModuleManager).IsAssignableFrom(type)) {
        throw new ResolutionSnapshotUnsupported("unsupported type " + type.FullName);
      }
      return;
    }
    if (type.Assembly == BaseTypes && type.IsValueType) { return; }
    if (type.IsGenericType) {
      var definition = type.GetGenericTypeDefinition();
      if (definition == typeof(Tuple<>) || definition == typeof(Tuple<,>) || definition == typeof(Tuple<,,>) ||
          definition == typeof(Tuple<,,,>) || definition == typeof(ValueTuple<>) || definition == typeof(ValueTuple<,>) ||
          definition == typeof(ValueTuple<,,>) || definition == typeof(ValueTuple<,,,>) || definition == typeof(KeyValuePair<,>) ||
          definition == typeof(ImmutableArray<>) || definition == typeof(Nullable<>)) {
        foreach (var argument in type.GetGenericArguments()) { RequireArgument(argument); }
        return;
      }
    }
    throw new ResolutionSnapshotUnsupported("unsupported type " + type.FullName);
  }

  private static void RequireArgument(System.Type type) {
    if (typeof(Delegate).IsAssignableFrom(type)) { throw new ResolutionSnapshotUnsupported("unsupported type argument " + type.FullName); }
  }

  public enum Collection : byte { List = 1, Dictionary = 2, Set = 3, SortedSet = 4, Stack = 5, ImmutableList = 6 }

  public static Collection? CollectionKind(System.Type type) {
    if (!type.IsGenericType) { return null; }
    var definition = type.GetGenericTypeDefinition();
    if (definition == typeof(List<>)) { return Collection.List; }
    if (definition == typeof(Dictionary<,>)) { return Collection.Dictionary; }
    if (definition == typeof(HashSet<>)) { return Collection.Set; }
    if (definition == typeof(SortedSet<>)) { return Collection.SortedSet; }
    if (definition == typeof(Stack<>)) { return Collection.Stack; }
    if (definition == typeof(ImmutableList<>)) { return Collection.ImmutableList; }
    return null;
  }

  /// <summary>
  /// The comparer of a collection: 0 the default, 1 ordinal strings, 2 reference identity, or the
  /// type of a stateless DafnyCore comparer (returned through <paramref name="comparerType"/>).
  /// </summary>
  public static byte Comparer(object collection, out System.Type? comparerType) {
    comparerType = null;
    var type = collection.GetType();
    if (CollectionKind(type) == Collection.ImmutableList) { return 0; }
    var property = type.GetProperty("Comparer");
    if (property == null) { return 0; }
    var comparer = property.GetValue(collection);
    if (ReferenceEquals(comparer, StringComparer.Ordinal)) { return 1; }
    if (ReferenceEquals(comparer, ReferenceEqualityComparer.Instance)) { return 2; }
    var generic = CollectionKind(type) == Collection.SortedSet ? typeof(Comparer<>) : typeof(EqualityComparer<>);
    var element = type.GetGenericArguments()[0];
    if (ReferenceEquals(comparer, generic.MakeGenericType(element).GetProperty("Default")!.GetValue(null))) { return 0; }
    var actual = comparer!.GetType();
    if (actual.Assembly == Core && !actual.ContainsGenericParameters && Fields(actual).Length == 0 &&
        actual.GetConstructor(System.Type.EmptyTypes) != null) {
      comparerType = actual;
      return 3;
    }
    throw new ResolutionSnapshotUnsupported("unsupported comparer " + actual.FullName);
  }

  public static object NewCollection(System.Type type, byte comparer, System.Type? comparerType) {
    var ops = Ops(type) ?? throw new ResolutionSnapshotInvalid("not a collection: " + type.FullName);
    if (ops.Kind == Collection.ImmutableList) {
      // Filled in place of the empty list (see ResolutionSnapshot.Materialize).
      return ops.New(null);
    }
    return comparer switch {
      0 => ops.New(null),
      1 => ops.New(StringComparer.Ordinal),
      2 => ops.New(ReferenceEqualityComparer.Instance),
      3 when comparerType != null && comparerType.Assembly == Core && Fields(comparerType).Length == 0 =>
        ops.New(Activator.CreateInstance(comparerType)),
      _ => throw new ResolutionSnapshotInvalid("unknown comparer")
    };
  }

  /// <summary>
  /// How materialization builds and fills a collection type, without reflection per item: a factory taking the
  /// comparer (or null for the default), an adder that fails on a duplicate set item, and for an immutable list
  /// the conversion of the filled list. Memoized per type.
  /// </summary>
  public sealed record CollectionOps(Collection Kind, Func<object?, object> New, Action<object, object?> Add, Func<object, object>? Freeze);

  private static readonly ConcurrentDictionary<System.Type, CollectionOps?> OpsCache = new();

  public static CollectionOps? Ops(System.Type type) => OpsCache.GetOrAdd(type, t => {
    if (CollectionKind(t) is not { } kind) { return null; }
    var arguments = t.GetGenericArguments();
    if (kind == Collection.Dictionary) {
      return (CollectionOps)typeof(DictionaryOps<,>).MakeGenericType(arguments).GetMethod(nameof(DictionaryOps<object, object>.Make))!.Invoke(null, null)!;
    }
    return (CollectionOps)typeof(OpsOf<>).MakeGenericType(arguments[0]).GetMethod(nameof(OpsOf<object>.Make))!.Invoke(null, [kind])!;
  });

  private static class DictionaryOps<TKey, TValue> where TKey : notnull {
    // Entries are added in pairs through IDictionary; Add here is never called.
    public static CollectionOps Make() => new(Collection.Dictionary,
      comparer => comparer == null ? new Dictionary<TKey, TValue>() : new Dictionary<TKey, TValue>((IEqualityComparer<TKey>)comparer),
      (_, _) => throw new InvalidOperationException(), null);
  }

  private static class OpsOf<T> {
    public static CollectionOps Make(Collection kind) => kind switch {
      Collection.List => new(kind, _ => new List<T>(), (list, value) => ((List<T>)list).Add((T)value!), null),
      Collection.ImmutableList => new(kind, _ => new List<T>(), (list, value) => ((List<T>)list).Add((T)value!),
        list => ImmutableList.CreateRange((List<T>)list)),
      Collection.Stack => new(kind, _ => new Stack<T>(), (stack, value) => ((Stack<T>)stack).Push((T)value!), null),
      Collection.Set => new(kind, comparer => comparer == null ? new HashSet<T>() : new HashSet<T>((IEqualityComparer<T>)comparer),
        (set, value) => { if (!((HashSet<T>)set).Add((T)value!)) { throw new ResolutionSnapshotInvalid("duplicate set item"); } }, null),
      Collection.SortedSet => new(kind, comparer => comparer == null ? new SortedSet<T>() : new SortedSet<T>((IComparer<T>)comparer),
        (set, value) => { if (!((SortedSet<T>)set).Add((T)value!)) { throw new ResolutionSnapshotInvalid("duplicate set item"); } }, null),
      _ => throw new InvalidOperationException()
    };
  }

  /// <summary>Resolve a type name of a snapshot, among the loaded assemblies only.</summary>
  // Every snapshot names its types, and resolving a name searches the loaded assemblies.
  private static readonly ConcurrentDictionary<string, System.Type?> ResolvedTypes = new(StringComparer.Ordinal);

  public static System.Type ResolveType(string name) {
    var type = ResolvedTypes.GetOrAdd(name, static name => {
      try {
        return System.Type.GetType(name, assemblyName => AppDomain.CurrentDomain.GetAssemblies()
          .FirstOrDefault(a => a.GetName().FullName == assemblyName.FullName), null, false);
      } catch (Exception error) when (error is not OutOfMemoryException) {
        return null;
      }
    });
    return type ?? throw new ResolutionSnapshotInvalid("unknown type " + name);
  }

  /// <summary>
  /// The runtime's shared empty arrays and immutable lists (<c>Array.Empty</c>, <c>ImmutableList.Empty</c>), which
  /// any module may hold. Like statics, they are bound by name, never copied.
  /// </summary>
  public static string? SingletonName(object value) {
    var type = value.GetType();
    if (value is Array { Length: 0 } array && type.GetElementType() is { } element && !element.ContainsGenericParameters &&
        ReferenceEquals(array, EmptyArray(element))) {
      return "empty-array:" + element.AssemblyQualifiedName;
    }
    if (value is Array { Length: 0 } inner && type.GetElementType() is { } innerElement && !innerElement.ContainsGenericParameters &&
        ReferenceEquals(inner, EmptyImmutableArray(innerElement))) {
      return "empty-immutable-array:" + innerElement.AssemblyQualifiedName;
    }
    if (CollectionKind(type) == Collection.ImmutableList && ((ICollection)value).Count == 0 &&
        ReferenceEquals(value, EmptyList(type.GetGenericArguments()[0]))) {
      return "empty-list:" + type.GetGenericArguments()[0].AssemblyQualifiedName;
    }
    return null;
  }

  public static object? Singleton(string name) {
    if (name.StartsWith("empty-array:", StringComparison.Ordinal)) { return EmptyArray(ResolveType(name["empty-array:".Length..])); }
    if (name.StartsWith("empty-immutable-array:", StringComparison.Ordinal)) {
      return EmptyImmutableArray(ResolveType(name["empty-immutable-array:".Length..]));
    }
    if (name.StartsWith("empty-list:", StringComparison.Ordinal)) { return EmptyList(ResolveType(name["empty-list:".Length..])); }
    return null;
  }

  // The singletons of an element type, found by reflection once.
  private static readonly ConcurrentDictionary<System.Type, object> EmptyArrays = new();
  private static readonly ConcurrentDictionary<System.Type, object?> EmptyImmutableArrays = new();
  private static readonly ConcurrentDictionary<System.Type, object> EmptyLists = new();

  private static object EmptyArray(System.Type element) => EmptyArrays.GetOrAdd(element, static element =>
    typeof(Array).GetMethod(nameof(Array.Empty))!.MakeGenericMethod(element).Invoke(null, null)!);

  /// <summary>The array inside <c>ImmutableArray.Empty</c>.</summary>
  private static object? EmptyImmutableArray(System.Type element) => EmptyImmutableArrays.GetOrAdd(element, static element => {
    var empty = typeof(ImmutableArray<>).MakeGenericType(element).GetField(nameof(ImmutableArray<int>.Empty))!.GetValue(null)!;
    return typeof(System.Runtime.InteropServices.ImmutableCollectionsMarshal).GetMethod(
      nameof(System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsArray))!.MakeGenericMethod(element).Invoke(null, [empty]);
  });

  private static object EmptyList(System.Type element) => EmptyLists.GetOrAdd(element, static element =>
    typeof(ImmutableList<>).MakeGenericType(element).GetField(nameof(ImmutableList<int>.Empty))!.GetValue(null)!);

  /// <summary>
  /// Static fields of DafnyCore syntax types, as singletons a resolved module may share
  /// (Token.NoToken, Type.Int). They are bound by name, never copied.
  /// </summary>
  public static IReadOnlyDictionary<object, string> Statics => statics.Value;
  public static IReadOnlyDictionary<string, object> StaticsByName => staticsByName.Value;

  private static readonly Lazy<Dictionary<object, string>> statics = new(() => {
    var result = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
    foreach (var (name, value) in staticsByName.Value.OrderBy(p => p.Key, StringComparer.Ordinal)) {
      result.TryAdd(value, name);
    }
    return result;
  });

  private static readonly Lazy<Dictionary<string, object>> staticsByName = new(() => {
    var roots = new[] { typeof(INode), typeof(Type), typeof(PreType), typeof(IOrigin), typeof(Token) };
    var result = new Dictionary<string, object>(StringComparer.Ordinal);
    System.Type[] types;
    try { types = Core.GetTypes(); } catch (ReflectionTypeLoadException error) { types = error.Types.Where(t => t != null).ToArray()!; }
    foreach (var type in types) {
      if (type.ContainsGenericParameters || !roots.Any(r => r.IsAssignableFrom(type)) ||
          type.Namespace?.StartsWith("Microsoft.Dafny.Compilers", StringComparison.Ordinal) == true) {
        continue;
      }
      foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) {
        if (field.IsLiteral || field.FieldType.IsValueType || field.FieldType == typeof(string) ||
            field.GetCustomAttribute<ThreadStaticAttribute>() != null) {
          continue;
        }
        if (field.GetValue(null) is { } value && !IsScalar(value.GetType())) {
          result[type.FullName + "." + field.Name] = value;
        }
      }
    }
    // The native types are shared elements of a static list, not static fields themselves.
    for (var i = 0; i < ModuleResolver.NativeTypes.Count; i++) {
      result[typeof(ModuleResolver).FullName + ".NativeTypes[" + i + "]"] = ModuleResolver.NativeTypes[i];
    }
    // A resolved @-attribute holds the default value of an omitted argument itself, and that value is shared.
    foreach (var attribute in Attributes.BuiltinAtAttributes) {
      foreach (var argument in attribute.Args) {
        if (argument.DefaultValue is { } value) {
          result[typeof(Attributes).FullName + ".BuiltinAtAttributes[" + attribute.Name + "]." + argument.ArgName] = value;
        }
      }
    }
    return result;
  });
}
