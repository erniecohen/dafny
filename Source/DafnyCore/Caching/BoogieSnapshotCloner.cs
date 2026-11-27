#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;
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
  private static readonly Lazy<Func<object, object>> MemberwiseCopy = new(() => Memberwise.CreateDelegate<Func<object, object>>());
  private static readonly FieldInfo? UniqueId = typeof(Bpl.Absy).GetField("<UniqueId>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
  private static readonly FieldInfo? ExpressionHash = typeof(Bpl.Expr).GetField("CachedHashCode", BindingFlags.Instance | BindingFlags.NonPublic);
  private static readonly ConcurrentDictionary<System.Type, FieldInfo[]> Fields = new();
  private static readonly ConcurrentDictionary<System.Type, Action<BoogieSnapshotCloner, object, object>> FieldCopiers = new();
  private readonly bool optimize;
  private static long optimizedClones;
  internal static long OptimizedClones => Interlocked.Read(ref optimizedClones);

  internal BoogieSnapshotCloner(IReadOnlyDictionary<SymbolBindingKey, ICanVerify>? currentSymbols, bool optimize) {
    this.currentSymbols = currentSymbols;
    this.optimize = optimize && RuntimeFeature.IsDynamicCodeSupported;
  }

  public static bool TryClone(Bpl.Program source, out Bpl.Program? copy, out string? unsupported,
    IReadOnlyDictionary<SymbolBindingKey, ICanVerify>? currentSymbols = null, bool optimize = false) {
    try {
      var cloner = new BoogieSnapshotCloner(currentSymbols, optimize);
      copy = (Bpl.Program)cloner.Copy(source)!;
      if (cloner.optimize) { Interlocked.Increment(ref optimizedClones); }
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
      return new CanVerifyOrigin(current) { IsReverifiedInheritedCheck = origin.IsReverifiedInheritedCheck };
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
    // Do not invoke virtual Clone: Type.Clone can return this or erase a resolved
    // proxy, neither of which is a graph copy. Allocate IDs through Absy's public
    // constructor, then install them in this pinned ABI's private copy.
    var clone = optimize ? MemberwiseCopy.Value(value) : Memberwise.Invoke(value, null)!;
    if (clone is Bpl.Absy) {
      if (UniqueId == null) { throw new UnsupportedSnapshotException("Boogie node identity ABI"); }
      UniqueId.SetValue(clone, new IdentityTicket().UniqueId);
    }
    copies.Add(value, clone);
    if (optimize) {
      FieldCopiers.GetOrAdd(type, CreateFieldCopier)(this, value, clone);
    } else {
      CopyFields(value, clone);
    }
    if (clone is Bpl.Expr { Immutable: true } expression) {
      if (ExpressionHash == null) { throw new UnsupportedSnapshotException("Boogie expression hash ABI"); }
      ExpressionHash.SetValue(expression, expression.ComputeHashCode());
    }
    return clone;
  }

  private void CopyFields(object source, object target) {
    foreach (var field in Fields.GetOrAdd(source.GetType(), CollectFields)) {
      field.SetValue(target, Copy(field.GetValue(source)));
    }
  }

  // Generate the same get -> recursive copy -> set sequence, in the same field order.
  // Statically known leaves need no recursive call: Copy returns those values unchanged.
  // The graph map, collection handling, fresh IDs and immutable-expression hashes stay
  // in Copy. Only metadata is cached: these delegates never retain a program or options.
  internal static Action<BoogieSnapshotCloner, object, object> CreateFieldCopier(System.Type type) {
    var fields = Fields.GetOrAdd(type, CollectFields);
    if (!RuntimeFeature.IsDynamicCodeSupported || Array.Exists(fields, field =>
          field.FieldType.IsPointer || field.FieldType.IsByRef || field.FieldType.IsByRefLike)) {
      return static (cloner, source, target) => cloner.CopyFields(source, target);
    }
    try {
      var method = new DynamicMethod("CopyBoogieFields", typeof(void),
        [typeof(BoogieSnapshotCloner), typeof(object), typeof(object)], typeof(BoogieSnapshotCloner).Module, true);
      var il = method.GetILGenerator();
      var copy = typeof(BoogieSnapshotCloner).GetMethod(nameof(Copy), BindingFlags.Instance | BindingFlags.NonPublic)!;
      foreach (var field in fields) {
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(field.DeclaringType!.IsValueType ? OpCodes.Unbox : OpCodes.Castclass, field.DeclaringType);
        var leaf = field.FieldType.IsPrimitive || field.FieldType.IsEnum || field.FieldType == typeof(string) ||
          field.FieldType == typeof(decimal) || field.FieldType == typeof(System.Numerics.BigInteger) ||
          field.FieldType == typeof(Microsoft.BaseTypes.BigNum) || field.FieldType == typeof(Microsoft.BaseTypes.BigDec) ||
          field.FieldType == typeof(Uri) || field.FieldType == typeof(System.Type);
        // In particular, tokens are not leaves here: CanVerifyOrigin must be rebound to
        // the current program before Copy's general token-sharing rule applies.
        if (!leaf) { il.Emit(OpCodes.Ldarg_0); }
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(field.DeclaringType.IsValueType ? OpCodes.Unbox : OpCodes.Castclass, field.DeclaringType);
        var isVolatile = Array.IndexOf(field.GetRequiredCustomModifiers(), typeof(IsVolatile)) >= 0;
        if (isVolatile) { il.Emit(OpCodes.Volatile); }
        il.Emit(OpCodes.Ldfld, field);
        if (!leaf) {
          if (field.FieldType.IsValueType) { il.Emit(OpCodes.Box, field.FieldType); }
          il.Emit(OpCodes.Call, copy);
          il.Emit(field.FieldType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, field.FieldType);
        }
        if (isVolatile) { il.Emit(OpCodes.Volatile); }
        il.Emit(OpCodes.Stfld, field);
      }
      il.Emit(OpCodes.Ret);
      return method.CreateDelegate<Action<BoogieSnapshotCloner, object, object>>();
    } catch (Exception error) when (error is MemberAccessException or NotSupportedException or ArgumentException) {
      // Failure to generate an accessor must not skip the original traversal, nor change
      // the point at which an unsupported child is discovered and fresh IDs are consumed.
      return static (cloner, source, target) => cloner.CopyFields(source, target);
    }
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
  private sealed class IdentityTicket() : Bpl.Absy(Bpl.Token.NoToken) {
    public override void Resolve(Bpl.ResolutionContext context) => throw new NotSupportedException();
    public override void Typecheck(Bpl.TypecheckingContext context) => throw new NotSupportedException();
  }
}
