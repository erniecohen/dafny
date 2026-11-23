#nullable enable
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny.Caching;

/// <summary>
/// A structural digest of a complete, unprepared Boogie graph: every field of every reachable
/// object, in field order, with a back-reference for each object already visited, so that both
/// content and sharing are covered. It is computed over the same graph, under the same leaf rules,
/// as <see cref="BoogieSnapshotCloner"/>, so a graph the copier can materialize has a digest, and
/// one it cannot has none. Two graphs with the same digest are copies of one another, up to the
/// node identities Boogie allocates (UniqueId), Boogie's memoized expression hashes, and the source
/// tokens and proof descriptions that are encoded by what they report: their location and text.
/// Nothing is reparsed: this is a key, never a loader.
/// </summary>
internal sealed class BoogieGraphFingerprint {
  private const string Domain = "boogie-graph-v1";
  private static readonly ConcurrentDictionary<System.Type, FieldInfo[]> Fields = new();
  private static readonly FieldInfo? ExpressionHash = typeof(Bpl.Expr).GetField("CachedHashCode", BindingFlags.Instance | BindingFlags.NonPublic);
  // A process-wide counter: its value records how many quantifiers this process made before, not
  // what this graph says. The SMT text depends only on its being set, unless debug information,
  // which prints it, is emitted.
  private static readonly FieldInfo SkolemId = typeof(Bpl.QuantifierExpr).GetField(nameof(Bpl.QuantifierExpr.SkolemId))!;
  private readonly Dictionary<object, int> visited = new(ReferenceEqualityComparer.Instance);
  private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
  private readonly StringBuilder? trace;
  private readonly bool skolemIdsPrinted;
  private readonly SourceLocations locations;

  private BoogieGraphFingerprint(bool skolemIdsPrinted, SourceLocations locations, StringBuilder? trace) {
    this.skolemIdsPrinted = skolemIdsPrinted;
    this.locations = locations;
    this.trace = trace;
  }

  /// <summary>
  /// The digest of <paramref name="program"/>, or null with a reason when it holds an object the
  /// copier does not support. <paramref name="skolemIdsPrinted"/> is whether the solver input
  /// prints quantifier skolem ids (Boogie's EmitDebugInformation). Source locations are encoded as
  /// <paramref name="locations"/> records them: files by content, positions relative to their
  /// enclosing declaration.
  /// </summary>
  public static string? TryCompute(Bpl.Program program, bool skolemIdsPrinted, SourceLocations locations, out string? unsupported,
    StringBuilder? trace = null) {
    var fingerprint = new BoogieGraphFingerprint(skolemIdsPrinted, locations, trace);
    try {
      fingerprint.Write(Domain);
      fingerprint.Visit(program);
      unsupported = null;
      return Convert.ToHexStringLower(fingerprint.hash.GetHashAndReset());
    } catch (UnsupportedGraphException error) {
      unsupported = error.Message;
      return null;
    } catch (Exception error) when (error is not OutOfMemoryException) {
      // Includes a description or token whose text cannot be computed: such a graph has no key.
      unsupported = "unsupported graph reflection operation: " + error.GetType().Name;
      return null;
    } finally {
      fingerprint.hash.Dispose();
    }
  }

  private void Visit(object? value) {
    if (value == null) { Write("null"); return; }
    var type = value.GetType();
    if (value is CanVerifyOrigin origin) {
      // The copier rebinds this to the current symbol of the same key.
      var key = locations.Symbol(origin.CanVerify);
      Write("can-verify"); Write(key.Type); Write(key.Name); Write(key.File);
      Write(key.StartLine); Write(key.StartColumn); Write(key.EndLine); Write(key.EndColumn);
      Write(origin.IsReverifiedInheritedCheck ? 1 : 0);
      Token(origin);
      return;
    }
    if (type.IsPrimitive || type.IsEnum || value is string or decimal or System.Numerics.BigInteger or
        Microsoft.BaseTypes.BigNum or Microsoft.BaseTypes.BigDec or Uri) {
      Write("leaf"); Write(type.FullName!); Write(Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""); return;
    }
    if (value is System.Type typeValue) { Write("type"); Write(typeValue.AssemblyQualifiedName ?? typeValue.FullName ?? typeValue.Name); return; }
    if (value is Bpl.IToken token) { Token(token); return; }
    if (value is Bpl.ProofObligationDescription description) {
      Write("description"); Write(type.FullName!); Write(description.SuccessDescription); Write(description.FailureDescription);
      Write(description.ShortDescription);
      return;
    }
    if (ReferenceEquals(value, Bpl.Expr.True)) { Write("true"); return; }
    if (ReferenceEquals(value, Bpl.Expr.False)) { Write("false"); return; }
    if (value is Bpl.BasicType or Bpl.BvType) { Write("shared-type"); Write(type.FullName!); Write(value.ToString() ?? ""); return; }
    if (visited.TryGetValue(value, out var index)) { Write("ref"); Write(index); return; }
    visited.Add(value, visited.Count);
    if (value is Array array) {
      if (array.Rank != 1 || array.GetLowerBound(0) != 0) { throw Unsupported(type); }
      Write("array"); Write(type.FullName!); Write(array.Length);
      for (var i = 0; i < array.Length; i++) { Visit(array.GetValue(i)); }
      return;
    }
    if (type.IsGenericType && type.GetGenericTypeDefinition() is var definition &&
        (definition == typeof(List<>) || definition == typeof(Dictionary<,>) || definition == typeof(HashSet<>) ||
         definition == typeof(ConcurrentStack<>))) {
      if (definition == typeof(Dictionary<,>) || definition == typeof(HashSet<>)) {
        var comparer = type.GetProperty("Comparer")!.GetValue(value);
        Write("comparer"); Write(comparer?.GetType().FullName ?? "");
      }
      Write("collection"); Write(type.FullName!);
      var count = 0;
      foreach (var _ in (IEnumerable)value) { count++; }
      Write(count);
      if (value is IDictionary dictionary) {
        foreach (DictionaryEntry item in dictionary) { Visit(item.Key); Visit(item.Value); }
      } else {
        foreach (var item in (IEnumerable)value) { Visit(item); }
      }
      return;
    }
    if (type.Assembly != typeof(Bpl.Program).Assembly && type.Assembly != typeof(Microsoft.BaseTypes.BigNum).Assembly) {
      throw Unsupported(type);
    }
    Write("object"); Write(type.FullName!);
    foreach (var field in Fields.GetOrAdd(type, CollectFields)) {
      if (field == SkolemId && !skolemIdsPrinted) { Write("skolem-id"); Write((int)field.GetValue(value)! == -1 ? 0 : 1); continue; }
      Visit(field.GetValue(value));
    }
  }

  private void Token(Bpl.IToken token) {
    Write("token"); Write(token.GetType().FullName!);
    if (token is IOrigin dafny) {
      Position(dafny.Uri, dafny.line, dafny.pos); Write(dafny.col);
      var range = dafny.ReportingRange;
      Position(range.StartToken.Uri, range.StartToken.line, range.StartToken.pos); Write(range.StartToken.col);
      if (range.EndToken is { } end) { Position(end.Uri ?? range.StartToken.Uri, end.line, end.pos); Write(end.col); } else { Write("no-end"); }
      Write(dafny.val ?? "");
      Write(dafny.IsCopy ? 1 : 0);
    } else if (string.IsNullOrEmpty(token.filename) || token.filename.StartsWith('<')) {
      // A Boogie token that names no source: Token.NoToken, or one a TokenTextWriter made for a
      // position in its own buffer. Boogie's printers that set tokens (as its lambda lifting does)
      // retarget the shared static types to such tokens, so which one a fresh translation holds
      // depends on what this process printed before. Neither locates anything that a report of
      // a module whose results are replayed (only modules that reported nothing) could name.
      Write("placeholder");
    } else if (locations.File(token.filename) is { } file) {
      // A Boogie token copied from a source token (as VCGeneration's ImplicitJump is).
      Write("source"); Position(file, token.line, token.pos); Write(token.col); Write(token.val ?? "");
    } else {
      Write(token.filename); Write(token.pos); Write(token.line); Write(token.col); Write(token.val ?? "");
    }
  }

  private void Position(Uri? uri, int line, int pos) {
    var (enclosing, relativeLine, relativePos) = locations.Relative(uri, line, pos);
    if (enclosing.Length != 0) { Write("declaration"); Write(enclosing); } else { Write("file"); Write(locations.Label(uri)); }
    Write(relativeLine); Write(relativePos);
  }

  private static FieldInfo[] CollectFields(System.Type type) {
    var result = new List<FieldInfo>();
    for (var current = type; current != null; current = current.BaseType) {
      foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) {
        // Process-local node identities and memoized hashes are recomputed on materialization.
        if (current == typeof(Bpl.Absy) && field.Name == "<UniqueId>k__BackingField") { continue; }
        if (ExpressionHash != null && field == ExpressionHash) { continue; }
        result.Add(field);
      }
    }
    return result.ToArray();
  }

  private void Write(string text) {
    var bytes = Encoding.UTF8.GetBytes(text);
    Span<byte> length = stackalloc byte[4];
    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
    hash.AppendData(length);
    hash.AppendData(bytes);
    trace?.Append(text).Append('\n');
  }

  private void Write(int number) {
    Span<byte> bytes = stackalloc byte[4];
    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes, number);
    hash.AppendData(bytes);
    trace?.Append('#').Append(number.ToString(CultureInfo.InvariantCulture)).Append('\n');
  }

  private static UnsupportedGraphException Unsupported(System.Type type) => new(type.FullName ?? type.Name);
  private sealed class UnsupportedGraphException(string message) : Exception(message);
}
