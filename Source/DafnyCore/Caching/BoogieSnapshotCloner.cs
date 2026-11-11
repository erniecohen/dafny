#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny;

/// <summary>
/// Copies the complete pre-preparation Boogie graph, preserving its reference topology.
/// Source tokens and proof descriptions are reporting-only anchors. Unknown object types
/// cause a miss. This is an in-process copier, never an on-disk deserializer.
/// </summary>
internal sealed class BoogieSnapshotCloner {
  private readonly Dictionary<object, object> copies = new(ReferenceEqualityComparer.Instance);
  internal readonly record struct SymbolBindingKey(System.Type Type, string Name, Uri? Uri, int StartLine, int StartColumn, int EndLine, int EndColumn);
  private readonly IReadOnlyDictionary<SymbolBindingKey, ICanVerify>? currentSymbols;
  private static readonly MethodInfo Memberwise = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;
  private static readonly ConcurrentDictionary<System.Type, FieldInfo[]> Fields = new();

  private BoogieSnapshotCloner(IReadOnlyDictionary<SymbolBindingKey, ICanVerify>? currentSymbols) {
    this.currentSymbols = currentSymbols;
  }

  public static bool TryClone(Bpl.Program source, out Bpl.Program? copy, out string? unsupported,
    IReadOnlyDictionary<SymbolBindingKey, ICanVerify>? currentSymbols = null) {
    try {
      copy = (Bpl.Program)new BoogieSnapshotCloner(currentSymbols).Copy(source)!;
      unsupported = null;
      return true;
    } catch (UnsupportedSnapshotException error) {
      copy = null;
      unsupported = error.Message;
      return false;
    } catch (Exception error) when (error is MemberAccessException or NotSupportedException or ArgumentException ||
                                 error is TargetInvocationException { InnerException: MemberAccessException or NotSupportedException or ArgumentException }) {
      copy = null;
      unsupported = "unsupported snapshot reflection operation";
      return false;
    }
  }

  private object? Copy(object? value) {
    if (value == null) { return null; }
    var type = value.GetType();
    // Task attribution is reference-based in Compilation; a token from an older
    // resolved program would silently leave the current symbol with zero tasks.
    if (value is CanVerifyOrigin origin && currentSymbols != null) {
      if (!currentSymbols.TryGetValue(SymbolKey(origin.CanVerify), out var current)) {
        throw new UnsupportedSnapshotException("current verification symbol binding");
      }
      return new CanVerifyOrigin(current);
    }
    if (type.IsPrimitive || type.IsEnum || value is string or decimal or System.Numerics.BigInteger or
        Microsoft.BaseTypes.BigNum or Microsoft.BaseTypes.BigDec or Uri or System.Type or Bpl.IToken or Bpl.ProofObligationDescription) {
      return value;
    }
    // These shared constants participate in Boogie's identity-sensitive literal rules.
    if (ReferenceEquals(value, Bpl.Expr.True) || ReferenceEquals(value, Bpl.Expr.False) || value is Bpl.BasicType or Bpl.BvType) {
      return value;
    }
    if (copies.TryGetValue(value, out var found)) { return found; }
    if (value is Array array) {
      if (array.Rank != 1 || array.GetLowerBound(0) != 0) { throw Unsupported(type); }
      var result = Array.CreateInstance(type.GetElementType()!, array.Length);
      copies.Add(value, result);
      for (var index = 0; index < array.Length; index++) { result.SetValue(Copy(array.GetValue(index)), index); }
      return result;
    }
    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) {
      var result = (IList)Activator.CreateInstance(type)!;
      copies.Add(value, result);
      foreach (var item in (IEnumerable)value) { result.Add(Copy(item)); }
      return result;
    }
    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>)) {
      var comparer = type.GetProperty("Comparer")!.GetValue(value);
      RequireDefaultComparer(comparer, type.GetGenericArguments()[0]);
      var result = (IDictionary)Activator.CreateInstance(type, comparer)!;
      copies.Add(value, result);
      foreach (DictionaryEntry item in (IDictionary)value) { result.Add(Copy(item.Key)!, Copy(item.Value)); }
      return result;
    }
    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>)) {
      var comparer = type.GetProperty("Comparer")!.GetValue(value);
      RequireDefaultComparer(comparer, type.GetGenericArguments()[0]);
      var result = Activator.CreateInstance(type, comparer)!;
      copies.Add(value, result);
      var add = type.GetMethod("Add")!;
      foreach (var item in (IEnumerable)value) { add.Invoke(result, [Copy(item)]); }
      return result;
    }
    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ConcurrentStack<>)) {
      var result = Activator.CreateInstance(type)!;
      copies.Add(value, result);
      var items = new List<object?>();
      foreach (var item in (IEnumerable)value) { items.Add(Copy(item)); }
      var push = type.GetMethod("Push")!;
      for (var index = items.Count - 1; index >= 0; index--) { push.Invoke(result, [items[index]]); }
      return result;
    }
    if (type.Assembly != typeof(Bpl.Program).Assembly && type.Assembly != typeof(Microsoft.BaseTypes.BigNum).Assembly) {
      throw Unsupported(type);
    }
    // Memberwise copying followed by field-wise traversal preserves fields that a syntax
    // visitor omits (definition-axiom owners, attributes, type links and literal aliases).
    // Boogie's Clone allocates a fresh Absy UniqueId, required by its functional maps.
    var clone = value is Bpl.Absy absy ? absy.Clone() : Memberwise.Invoke(value, null)!;
    copies.Add(value, clone);
    foreach (var field in Fields.GetOrAdd(type, CollectFields)) {
      field.SetValue(clone, Copy(field.GetValue(value)));
    }
    return clone;
  }

  private static FieldInfo[] CollectFields(System.Type type) {
    var result = new List<FieldInfo>();
    for (var current = type; current != null; current = current.BaseType) {
      foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) {
        if (current != typeof(Bpl.Absy) || field.Name != "<UniqueId>k__BackingField") { result.Add(field); }
      }
    }
    return result.ToArray();
  }

  private static void RequireDefaultComparer(object? comparer, System.Type keyType) {
    var defaultComparer = typeof(EqualityComparer<>).MakeGenericType(keyType).GetProperty("Default")!.GetValue(null);
    if (!ReferenceEquals(comparer, defaultComparer) && !ReferenceEquals(comparer, StringComparer.Ordinal) &&
        !ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase) && !ReferenceEquals(comparer, ReferenceEqualityComparer.Instance) &&
        !(comparer is Bpl.Implementation.ChecksumComparer && Fields.GetOrAdd(comparer.GetType(), CollectFields).Length == 0)) {
      throw new UnsupportedSnapshotException("unsupported collection comparer: " + comparer?.GetType().FullName);
    }
  }

  private static UnsupportedSnapshotException Unsupported(System.Type type) => new(type.FullName ?? type.Name);
  internal static SymbolBindingKey SymbolKey(ICanVerify symbol) => new(symbol.GetType(), symbol.FullDafnyName, symbol.Origin.Uri,
    symbol.StartToken.line, symbol.StartToken.col, symbol.EndToken.line, symbol.EndToken.col);
  private sealed class UnsupportedSnapshotException(string message) : Exception(message);
}
