#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Dafny;

/// <summary>An object outside a module's own graph: a static, a named context object, or an object of a prerequisite or of a builtin.</summary>
internal readonly record struct ResolutionExternal(ResolutionExternal.Kinds Kind, string Name, int Index) {
  internal enum Kinds : byte { Static = 1, Named = 2, Prerequisite = 3, Anchor = 4 }
}

/// <summary>A visibility-scope token of a prerequisite or of a builtin: the rank among that owner's tokens.</summary>
internal readonly record struct ResolutionExternalToken(ResolutionExternal.Kinds Kind, string Name, int Rank);

/// <summary>
/// The tokens of the parsed program, so that a snapshot refers to a source token by its file and
/// offset instead of copying it. A token whose file, offset and text are not unique is not indexed.
/// </summary>
internal sealed class ResolutionTokenIndex {
  private readonly Dictionary<Token, (string Uri, int Pos)> positions = new(ReferenceEqualityComparer.Instance);
  private readonly Dictionary<(string Uri, int Pos, string Val), Token> tokens = new();

  public static ResolutionTokenIndex For(Program program, IEnumerable<INode> extraRoots) {
    var index = new ResolutionTokenIndex();
    var visited = new HashSet<Token>(ReferenceEqualityComparer.Instance);
    var ambiguous = new HashSet<(string, int, string)>();
    void Walk(Token? start) {
      if (start == null || ReferenceEquals(start, Token.NoToken) || visited.Contains(start)) { return; }
      var first = start;
      var guard = 0;
      while (first.Prev != null && !visited.Contains(first.Prev) && guard++ < 100_000_000) { first = first.Prev; }
      for (var token = first; token != null && visited.Add(token); token = token.Next) {
        if (token.Uri == null) { continue; }
        var key = (token.Uri.ToString(), token.pos, token.val ?? "");
        if (!index.tokens.TryAdd(key, token)) { ambiguous.Add(key); }
      }
    }
    foreach (var file in program.Files) {
      Walk(file.EntireRange?.StartToken);
      Walk(file.Origin.EntireRange?.StartToken);
      Walk(file.Origin.ReportingRange.StartToken);
    }
    foreach (var node in extraRoots) { Walk(node.EntireRange?.StartToken); }
    foreach (var key in ambiguous) { index.tokens.Remove(key); }
    foreach (var (key, token) in index.tokens) { index.positions[token] = (key.Uri, key.Pos); }
    return index;
  }

  public bool TryGet(Token token, out (string Uri, int Pos) position) => positions.TryGetValue(token, out position);

  public Token Find(string uri, int pos, string val) =>
    tokens.TryGetValue((uri, pos, val), out var token) ? token : throw new ResolutionSnapshotInvalid("no source token " + val + " at " + pos);
}

/// <summary>
/// The pieces of every module's own source (see ProgramResolver.OwnSource), so that a snapshot locates a token by
/// its module, piece and offset in the piece instead of by its file and absolute offset. Pieces do not overlap.
/// </summary>
internal sealed class ResolutionFrames {
  public readonly record struct Piece(string Module, int Index, Token Start, Token End);

  private readonly Dictionary<string, Piece[]> byUri = new(StringComparer.Ordinal);
  private readonly Dictionary<string, Piece[]> byModule = new(StringComparer.Ordinal);
  private readonly HashSet<Uri> files = new();

  public ResolutionFrames(IEnumerable<(string Module, (Token Start, Token End)[] Pieces)> modules) {
    var lists = new Dictionary<string, List<Piece>>(StringComparer.Ordinal);
    foreach (var (module, pieces) in modules) {
      var own = pieces.Select((p, i) => new Piece(module, i, p.Start, p.End)).ToArray();
      byModule[module] = own;
      foreach (var piece in own) {
        if (piece.Start.Uri == null || piece.End.Uri != piece.Start.Uri) { continue; }
        files.Add(piece.Start.Uri);
        var uri = piece.Start.Uri.ToString();
        if (!lists.TryGetValue(uri, out var list)) { lists[uri] = list = []; }
        list.Add(piece);
      }
    }
    foreach (var (uri, list) in lists) {
      byUri[uri] = list.OrderBy(p => p.Start.pos).ToArray();
    }
  }

  /// <summary>Whether <paramref name="text"/> contains the path or URI of a source file.</summary>
  public bool NamesSourceFile(string text) {
    if (text.IndexOfAny(['/', '\\']) < 0) { return false; }
    foreach (var file in files) {
      if (text.Contains(file.OriginalString, StringComparison.Ordinal) ||
          (file.IsFile && text.Contains(file.LocalPath, StringComparison.Ordinal))) {
        return true;
      }
    }
    return false;
  }

  /// <summary>Whether <paramref name="uri"/> is the file of a piece of some module.</summary>
  public bool IsSourceFile(Uri uri) => files.Contains(uri);

  /// <summary>The piece that contains <paramref name="pos"/> of <paramref name="uri"/>, if any.</summary>
  public Piece? Find(string uri, int pos) {
    if (!byUri.TryGetValue(uri, out var pieces)) { return null; }
    int low = 0, high = pieces.Length - 1, found = -1;
    while (low <= high) {
      var middle = (low + high) / 2;
      if (pieces[middle].Start.pos <= pos) { found = middle; low = middle + 1; } else { high = middle - 1; }
    }
    return found >= 0 && pos <= pieces[found].End.pos + (pieces[found].End.val?.Length ?? 0) ? pieces[found] : null;
  }

  /// <summary>
  /// The piece that contains <paramref name="line"/> and <paramref name="col"/> of <paramref name="uri"/>, if any: the
  /// place of a token that resolution made from another's line and column but not its offset.
  /// </summary>
  public Piece? FindLine(string uri, int line, int col) {
    if (!byUri.TryGetValue(uri, out var pieces)) { return null; }
    foreach (var piece in pieces) {
      if ((piece.Start.line, piece.Start.col).CompareTo((line, col)) <= 0 &&
          (line, col).CompareTo((piece.End.line, piece.End.col + (piece.End.val?.Length ?? 0))) <= 0) {
        return piece;
      }
    }
    return null;
  }

  public Piece[] Of(string module) => byModule.GetValueOrDefault(module) ?? [];
}

/// <summary>
/// The reference graph of a resolved module (issue 261): its declaration, signatures, member tables and
/// translation inputs, as a list of entries in breadth-first order. An entry is the type and data fields of
/// an object, the items of a collection or array, or the tokens of a visibility scope. A value is a scalar,
/// another entry, an external (see <see cref="ResolutionExternal"/>) or a source token. The structure digest
/// is the digest of the encoding with every source coordinate set to zero: two snapshots with the same
/// structure digest differ at most in where their tokens are.
/// </summary>
internal static class ResolutionSnapshot {
  // Resolved compiler modules include the expanded match trees, which can exceed four million objects.
  // Counts are also bounded by the remaining payload bytes before any materialization array is allocated.
  public const int MaxObjects = 16_000_000;
  private const string StructureDomain = "dafny-resolution-structure-v1";

  private enum EntryKind : byte { Fields = 1, Array = 2, Collection = 3, Scope = 4, Lock = 5 }

  private enum Tag : byte {
    Null = 0, False = 1, True = 2, Int32 = 3, Int64 = 4, String = 5, Char = 6, Double = 7, Byte = 8, SByte = 9,
    Int16 = 10, UInt16 = 11, UInt32 = 12, UInt64 = 13, Single = 14, Decimal = 15, BigInteger = 16, Uri = 17, Guid = 18,
    Enum = 19, Reference = 30, External = 31, SourceToken = 32, PieceUri = 33, AnchorUri = 34
  }

  private enum TokenOwner : byte { Own = 0, Prerequisite = 1 }

  public sealed class CaptureWorld {
    /// <summary>The external an object is, or null for an object of the module's own graph.</summary>
    public required Func<object, ResolutionExternal?> Object { get; init; }
    /// <summary>The external a scope token is, or null for a token of the module itself.</summary>
    public required Func<uint, ResolutionExternalToken?> Token { get; init; }
    public required ResolutionTokenIndex SourceTokens { get; init; }
    /// <summary>The module whose pieces of source are its own.</summary>
    public required string Module { get; init; }
    public required ResolutionFrames Frames { get; init; }
    /// <summary>The rank of a source token of a prerequisite among the tokens its result refers to, or null.</summary>
    public required Func<string, Token, int?> PrerequisiteToken { get; init; }
  }

  /// <param name="SourceTokens">The tokens of the module's own source that the snapshot refers to, in order of first reference.</param>
  public sealed record Captured(byte[] Payload, string Structure, object[] Owned, uint[] OwnedTokens, HashSet<string> Anchors, Token[] SourceTokens);

  public sealed class MaterializeWorld {
    public required DafnyOptions Options { get; init; }
    public required Func<ResolutionExternal, object> Object { get; init; }
    public required Func<ResolutionExternalToken, uint> Token { get; init; }
    public required ResolutionTokenIndex SourceTokens { get; init; }
    public required string Module { get; init; }
    public required ResolutionFrames Frames { get; init; }
    public required Func<string, int, Token> PrerequisiteToken { get; init; }
  }

  public sealed record Materialized(object?[] Roots, object[] Owned, uint[] OwnedTokens, Token[] SourceTokens);

  /// <summary>The relative offset of a copied token whose offset resolution left at 0.</summary>
  private const int NoOffset = int.MinValue;

  /// <summary>
  /// Whether resolution made <paramref name="token"/> from a line and column without an offset. Only the first token
  /// of a file has offset 0 at line 1, column 1.
  /// </summary>
  private static bool HasNoOffset(Token token) => token.pos == 0 && (token.line, token.col) != (1, 1);

  /// <summary>The fields of a token that say where it is, which a copied token stores relative to its piece.</summary>
  private static readonly int[] TokenPlace = new[] { "<Uri>k__BackingField", "<pos>k__BackingField", "<line>k__BackingField", "<col>k__BackingField" }
    .Select(name => Array.FindIndex(ResolutionGraphSchema.Fields(typeof(Token)), f => f.Name == name)).ToArray();

  // ---------------------------------------------------------------- capture

  /// <param name="contextual">Objects whose contextual fields (<see cref="ResolutionGraphSchema.IsContextual"/>) are written as defaults.</param>
  public static Captured Capture(object?[] roots, CaptureWorld world, IReadOnlySet<object> contextual) {
    var ids = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
    var owned = new List<object>();
    var children = new List<object?[]>();
    var anchors = new HashSet<string>(StringComparer.Ordinal);
    var parents = new List<int>();
    var current = -1;

    // An object outside the module is referred to many times; the world is asked about it once.
    var externals = new Dictionary<object, ResolutionExternal?>(ReferenceEqualityComparer.Instance);
    ResolutionExternal? External(object value) {
      if (!externals.TryGetValue(value, out var external)) {
        external = world.Object(value);
        externals.Add(value, external);
      }
      return external;
    }
    bool IsLeaf(object value) => ResolutionGraphSchema.IsScalar(value.GetType()) ||
      External(value) != null || (value is Token token && world.SourceTokens.TryGet(token, out _));
    void Enqueue(object? value) {
      // An object already in the snapshot is not a leaf, so the cheap test comes first.
      if (value == null || ids.ContainsKey(value) || IsLeaf(value)) { return; }
      if (owned.Count >= MaxObjects) { throw new ResolutionSnapshotUnsupported("graph too large"); }
      ids.Add(value, owned.Count);
      owned.Add(value);
      parents.Add(current);
    }
    // The types on the path from a root to an object, which an unsupported graph names.
    string Path(int id) {
      var path = new List<string>();
      for (; id >= 0 && path.Count < 8; id = parents[id]) { path.Add(owned[id].GetType().Name); }
      return string.Join(" < ", path);
    }
    foreach (var root in roots) { Enqueue(root); }
    var scopeTokens = new SortedSet<uint>();
    for (var i = 0; i < owned.Count; i++) {
      var value = owned[i];
      current = i;
      try {
        var kids = Children(value, contextual.Contains(value));
        if (value is VisibilityScope scope) {
          foreach (var token in ResolutionGraphSchema.Tokens(scope)) {
            if (world.Token(token) == null) { scopeTokens.Add(token); }
          }
        }
        children.Add(kids);
        foreach (var kid in kids) { Enqueue(kid); }
      } catch (ResolutionSnapshotUnsupported error) when (!error.Message.Contains(" in ", StringComparison.Ordinal)) {
        throw new ResolutionSnapshotUnsupported(error.Message + " in " + Path(i));
      }
    }
    var logical = new Dictionary<uint, int>();
    foreach (var token in scopeTokens) { logical[token] = logical.Count; }

    var ownPieces = world.Frames.Of(world.Module);
    var ownTokenRanks = new Dictionary<Token, int>(ReferenceEqualityComparer.Instance);
    var ownTokens = new List<Token>();
    int? PieceOf(Uri uri) {
      for (var i = 0; i < ownPieces.Length; i++) {
        if (ownPieces[i].Start.Uri == uri) { return i; }
      }
      return null;
    }
    // Where a token that resolution made from a source token's coordinates is: in a piece of the module's own source,
    // or at a token of a prerequisite that the prerequisite's result refers to (a refinement copies the refined
    // module's text, and resolving the copy makes tokens there). Its coordinates are stored relative to that start.
    (Token Start, int Piece, string? Module, int Rank)? CopiedPlace(Token token) {
      if (token.Uri == null) { return null; }
      var noOffset = HasNoOffset(token);
      // Resolution makes some tokens from another's line and column only, leaving the offset at 0.
      var piece = noOffset ? world.Frames.FindLine(token.Uri.ToString(), token.line, token.col)
        : world.Frames.Find(token.Uri.ToString(), token.pos);
      if (piece is { } own && own.Module == world.Module) { return (own.Start, own.Index, null, -1); }
      if (piece is { } other) {
        for (var at = other.Start; at != null; at = at.Next) {
          if (noOffset ? (at.line, at.col) == (token.line, token.col) : at.pos == token.pos) {
            if (world.PrerequisiteToken(other.Module, at) is { } rank) { return (at, -1, other.Module, rank); }
            break;
          }
          if (at.pos >= other.End.pos) { break; }
        }
      }
      if (world.Frames.IsSourceFile(token.Uri)) { throw new ResolutionSnapshotUnsupported("copied token outside the module"); }
      return null;
    }

    var writer = new Writer();
    void WriteValue(object? value, bool positional = false) {
      switch (value) {
        case null: writer.Byte((byte)Tag.Null); return;
        case bool b: writer.Byte((byte)(b ? Tag.True : Tag.False)); return;
        case int n: writer.Byte((byte)Tag.Int32); writer.Int(n, positional); return;
        case string s: writer.Byte((byte)Tag.String); writer.String(s); return;
        case long n: writer.Byte((byte)Tag.Int64); writer.Int(n); return;
        case char c: writer.Byte((byte)Tag.Char); writer.Int(c); return;
        case double d: writer.Byte((byte)Tag.Double); writer.Int(BitConverter.DoubleToInt64Bits(d)); return;
        case byte n: writer.Byte((byte)Tag.Byte); writer.Int(n); return;
        case sbyte n: writer.Byte((byte)Tag.SByte); writer.Int(n); return;
        case short n: writer.Byte((byte)Tag.Int16); writer.Int(n); return;
        case ushort n: writer.Byte((byte)Tag.UInt16); writer.Int(n); return;
        case uint n: writer.Byte((byte)Tag.UInt32); writer.Int(n); return;
        case ulong n: writer.Byte((byte)Tag.UInt64); writer.Int(unchecked((long)n)); return;
        case float f: writer.Byte((byte)Tag.Single); writer.Int(BitConverter.SingleToInt32Bits(f)); return;
        case decimal m:
          writer.Byte((byte)Tag.Decimal);
          foreach (var part in decimal.GetBits(m)) { writer.Int(part); }
          return;
        case BigInteger big: writer.Byte((byte)Tag.BigInteger); writer.String(big.ToString(System.Globalization.CultureInfo.InvariantCulture)); return;
        case Uri uri:
          if (PieceOf(uri) is { } own) {
            writer.Byte((byte)Tag.PieceUri);
            writer.Int(own);
            return;
          }
          if (world.Frames.IsSourceFile(uri)) { throw new ResolutionSnapshotUnsupported("file of another module"); }
          writer.Byte((byte)Tag.Uri); writer.String(uri.OriginalString); writer.Int(uri.IsAbsoluteUri ? 1 : 0); return;
        case Guid guid: writer.Byte((byte)Tag.Guid); writer.String(guid.ToString("N")); return;
      }
      var type = value.GetType();
      if (type.IsEnum) {
        writer.Byte((byte)Tag.Enum);
        writer.Type(type, false);
        writer.Int(Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));
        return;
      }
      if (ids.TryGetValue(value, out var id)) {
        writer.Byte((byte)Tag.Reference);
        writer.Int(id);
        return;
      }
      if (External(value) is { } external) {
        writer.Byte((byte)Tag.External);
        writer.Byte((byte)external.Kind);
        writer.String(external.Name);
        writer.Int(external.Index);
        writer.Type(type, false);
        if (external.Kind == ResolutionExternal.Kinds.Anchor) { anchors.Add(external.Name); }
        return;
      }
      if (value is Token token && world.SourceTokens.TryGet(token, out var position)) {
        var piece = world.Frames.Find(position.Uri, position.Pos) ?? throw new ResolutionSnapshotUnsupported("token outside every module");
        writer.Byte((byte)Tag.SourceToken);
        if (piece.Module == world.Module) {
          // The rank, by first reference, says which references are to one token; the offset, where it is.
          if (!ownTokenRanks.TryGetValue(token, out var rank)) {
            rank = ownTokens.Count;
            ownTokenRanks.Add(token, rank);
            ownTokens.Add(token);
          }
          writer.Byte((byte)TokenOwner.Own);
          writer.Int(rank);
          writer.Int(piece.Index);
          writer.Int(position.Pos - piece.Start.pos, true);
        } else {
          var rank = world.PrerequisiteToken(piece.Module, token) ??
                     throw new ResolutionSnapshotUnsupported("token of " + piece.Module + " that its result does not refer to");
          writer.Byte((byte)TokenOwner.Prerequisite);
          writer.String(piece.Module);
          writer.Int(rank);
        }
        writer.String(token.val ?? "");
        return;
      }
      writer.Byte((byte)Tag.Reference);
      writer.Int(ids[value]);
    }

    writer.Int(owned.Count);
    for (var i = 0; i < owned.Count; i++) {
      var value = owned[i];
      var kids = children[i];
      switch (value) {
        case VisibilityScope scope:
          writer.Byte((byte)EntryKind.Scope);
          // A token's number depends on when it was allocated, and a prerequisite that was reused allocated its
          // tokens later than one resolved afresh. So the tokens are written in the order of their names.
          var tokens = ResolutionGraphSchema.Tokens(scope).Select(token => world.Token(token) is { } external
              ? (Kind: (byte)external.Kind, external.Name, external.Rank)
              : ((byte)0, "", logical[token]))
            .OrderBy(t => t.Item1).ThenBy(t => t.Item2, StringComparer.Ordinal).ThenBy(t => t.Item3).ToArray();
          writer.Int(tokens.Length);
          foreach (var (kind, name, rank) in tokens) {
            writer.Byte(kind);
            if (kind != 0) { writer.String(name); }
            writer.Int(rank);
            if (kind == (byte)ResolutionExternal.Kinds.Anchor) { anchors.Add(name); }
          }
          var names = ResolutionGraphSchema.Ids(scope);
          writer.Int(names.Count);
          foreach (var name in names) { writer.String(name); }
          continue;
        case Array array:
          writer.Byte((byte)EntryKind.Array);
          writer.Type(array.GetType(), false);
          writer.Int(kids.Length);
          foreach (var kid in kids) { WriteValue(kid); }
          continue;
      }
      var type = value.GetType();
      if (type == typeof(object)) {
        writer.Byte((byte)EntryKind.Lock);
        continue;
      }
      if (ResolutionGraphSchema.CollectionKind(type) != null) {
        writer.Byte((byte)EntryKind.Collection);
        writer.Type(type, false);
        var comparer = ResolutionGraphSchema.Comparer(value, out var comparerType);
        writer.Byte(comparer);
        if (comparerType != null) { writer.Type(comparerType, false); }
        writer.Int(kids.Length);
        foreach (var kid in kids) { WriteValue(kid); }
        continue;
      }
      writer.Byte((byte)EntryKind.Fields);
      writer.Type(type, true);
      var fields = ResolutionGraphSchema.Fields(type);
      if (type == typeof(Token) && CopiedPlace((Token)value) is { } place) {
        // Relative to the piece's start or the anchor: the offset, the line difference, and the column difference on its first line.
        var copy = (Token)value;
        var lines = copy.line - place.Start.line;
        kids = (object?[])kids.Clone();
        kids[TokenPlace[1]] = HasNoOffset(copy) ? NoOffset : copy.pos - place.Start.pos;
        kids[TokenPlace[2]] = lines;
        kids[TokenPlace[3]] = lines == 0 ? copy.col - place.Start.col : copy.col;
        for (var j = 0; j < fields.Length; j++) {
          if (j == TokenPlace[0] && place.Module == null) {
            writer.Byte((byte)Tag.PieceUri);
            writer.Int(place.Piece);
          } else if (j == TokenPlace[0]) {
            writer.Byte((byte)Tag.AnchorUri);
            writer.String(place.Module);
            writer.Int(place.Rank);
          } else {
            WriteValue(kids[j], ResolutionGraphSchema.IsPositional(fields[j]));
          }
        }
        continue;
      }
      for (var j = 0; j < fields.Length; j++) { WriteValue(kids[j], ResolutionGraphSchema.IsPositional(fields[j])); }
    }
    writer.Int(roots.Length);
    foreach (var root in roots) { WriteValue(root); }
    writer.Int(scopeTokens.Count);
    if (writer.Strings.FirstOrDefault(world.Frames.NamesSourceFile) is { } path) {
      throw new ResolutionSnapshotUnsupported("a string names a source file: " + path);
    }
    var (payload, structure) = writer.Finish();
    return new(payload, Digest(structure), owned.ToArray(), scopeTokens.ToArray(), anchors, ownTokens.ToArray());
  }

  private static string Digest(byte[] structure) {
    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    hash.AppendData(Encoding.UTF8.GetBytes(StructureDomain));
    hash.AppendData(structure);
    return Convert.ToHexStringLower(hash.GetHashAndReset());
  }

  /// <summary>The values an entry holds, in encoding order.</summary>
  internal static object?[] Children(object value, bool contextual = false) {
    switch (value) {
      case VisibilityScope:
        return [];
      case Array array:
        if (array.Rank != 1 || array.GetLowerBound(0) != 0) { throw new ResolutionSnapshotUnsupported("array shape"); }
        if (typeof(Delegate).IsAssignableFrom(array.GetType().GetElementType())) { throw new ResolutionSnapshotUnsupported("delegate array"); }
        return array.Cast<object?>().ToArray();
    }
    var type = value.GetType();
    if (type == typeof(object)) { return []; }
    if (ResolutionGraphSchema.CollectionKind(type) != null) {
      if (value is IDictionary dictionary) {
        var items = new List<object?>();
        foreach (DictionaryEntry pair in dictionary) { items.Add(pair.Key); items.Add(pair.Value); }
        return items.ToArray();
      }
      return ((IEnumerable)value).Cast<object?>().ToArray();
    }
    ResolutionGraphSchema.RequireDataType(type);
    var fields = ResolutionGraphSchema.Fields(type);
    var result = new object?[fields.Length];
    for (var i = 0; i < fields.Length; i++) {
      result[i] = contextual && ResolutionGraphSchema.IsContextual(fields[i])
        ? fields[i].FieldType.IsValueType ? Activator.CreateInstance(fields[i].FieldType) : null
        : fields[i].GetValue(value);
    }
    return result;
  }

  private sealed class Writer {
    private readonly MemoryStream bodyStream = new();
    private readonly MemoryStream structureStream = new();
    private readonly BinaryWriter body;
    private readonly BinaryWriter structure;
    private readonly Dictionary<string, int> strings = new(StringComparer.Ordinal);
    private readonly List<string> stringList = [];
    public IReadOnlyList<string> Strings => stringList;
    private readonly Dictionary<System.Type, int> types = new();
    private readonly List<(System.Type Type, bool Schema)> typeList = [];

    public Writer() {
      body = new BinaryWriter(bodyStream, Encoding.UTF8, true);
      structure = new BinaryWriter(structureStream, Encoding.UTF8, true);
    }

    public void Byte(byte b) { body.Write(b); structure.Write(b); }
    public void Int(long n, bool positional = false) {
      body.Write7BitEncodedInt64(n);
      structure.Write7BitEncodedInt64(positional ? 0 : n);
    }
    public void String(string s) {
      if (!strings.TryGetValue(s, out var index)) {
        index = stringList.Count;
        strings.Add(s, index);
        stringList.Add(s);
      }
      Int(index);
    }
    public void Type(System.Type type, bool schema) {
      if (!types.TryGetValue(type, out var index)) {
        index = typeList.Count;
        types.Add(type, index);
        typeList.Add((type, schema));
      } else if (schema && !typeList[index].Schema) {
        typeList[index] = (type, true);
      }
      Int(index);
    }

    public (byte[] Payload, byte[] Structure) Finish() {
      body.Flush();
      structure.Flush();
      using var tables = new MemoryStream();
      using (var writer = new BinaryWriter(tables, Encoding.UTF8, true)) {
        writer.Write7BitEncodedInt(stringList.Count);
        foreach (var s in stringList) { writer.Write(s); }
        writer.Write7BitEncodedInt(typeList.Count);
        foreach (var (type, schema) in typeList) {
          writer.Write(type.AssemblyQualifiedName!);
          writer.Write(schema);
          if (schema) {
            var fields = ResolutionGraphSchema.Fields(type);
            writer.Write7BitEncodedInt(fields.Length);
            foreach (var field in fields) { writer.Write(ResolutionGraphSchema.Name(field)); }
          }
        }
      }
      var prefix = tables.ToArray();
      return ([.. prefix, .. bodyStream.ToArray()], [.. prefix, .. structureStream.ToArray()]);
    }
  }

  // ---------------------------------------------------------------- materialize

  /// <param name="Piece">For a URI stored as a piece of the module's own source, the piece; otherwise -1.</param>
  private readonly record struct Value(int Reference, object? Data, int Piece = -1);

  // An entry's values are the slice [Start, Start + Count) of one array shared by all entries of a payload.
  private struct Entry {
    public EntryKind Kind;
    public System.Type Type;
    public int Start, Count;
    public ScopeEntry? Scope;
  }

  private sealed record ScopeEntry((ResolutionExternal.Kinds Kind, string Name, int Rank)[] Tokens, string[] Names);

  /// <summary>
  /// The entry and value arrays of one materialization, reused by the next on the same thread: they are
  /// as large as the payload, and allocating them per module was most of a warm run's garbage.
  /// </summary>
  private sealed class Scratch {
    public Entry[] Entries = [];
    public Value[] Values = new Value[1 << 12];
    public int Used, EntryCount;
    public const int Retained = 1 << 20;
  }

  [ThreadStatic] private static Scratch? pooledScratch;

  public static Materialized Materialize(byte[] payload, MaterializeWorld world) {
    var scratch = pooledScratch ?? new Scratch();
    pooledScratch = null;
    try {
      return Materialize(payload, world, scratch);
    } finally {
      Array.Clear(scratch.Entries, 0, Math.Min(scratch.EntryCount, scratch.Entries.Length));
      scratch.EntryCount = 0;
      Array.Clear(scratch.Values, 0, Math.Min(scratch.Used, scratch.Values.Length));
      scratch.Used = 0;
      if (scratch.Entries.Length <= Scratch.Retained && scratch.Values.Length <= Scratch.Retained) { pooledScratch = scratch; }
    }
  }

  private const int CollectionMask = 0xff, LazyBit = 0x100, HashedBit = 0x200;
  private static readonly object BoxedFalse = false, BoxedTrue = true;

  private static Materialized Materialize(byte[] payload, MaterializeWorld world, Scratch scratch) {
    using var stream = new MemoryStream(payload, false);
    using var reader = new BinaryReader(stream, Encoding.UTF8);
    int Count(long limit) {
      var n = reader.Read7BitEncodedInt();
      if (n < 0 || n > limit) { throw new ResolutionSnapshotInvalid("bad count"); }
      return n;
    }
    var remaining = () => stream.Length - stream.Position;
    var strings = new string[Count(remaining())];
    for (var i = 0; i < strings.Length; i++) { strings[i] = reader.ReadString(); }
    var types = new System.Type[Count(remaining())];
    var schemas = new bool[types.Length];
    for (var i = 0; i < types.Length; i++) {
      types[i] = ResolutionGraphSchema.ResolveType(reader.ReadString());
      schemas[i] = reader.ReadBoolean();
      if (schemas[i]) {
        ResolutionGraphSchema.RequireDataType(types[i]);
        var fields = ResolutionGraphSchema.Fields(types[i]);
        var n = Count(remaining());
        if (n != fields.Length) { throw new ResolutionSnapshotInvalid("field schema of " + types[i].FullName); }
        for (var j = 0; j < n; j++) {
          if (reader.ReadString() != ResolutionGraphSchema.Name(fields[j])) {
            throw new ResolutionSnapshotInvalid("field schema of " + types[i].FullName);
          }
        }
      }
    }
    PipelineProfile.ModulePhase(world.Options, "cache.materialize.schema");
    long Int() => reader.Read7BitEncodedInt64();
    string Str() {
      var index = Int();
      if (index < 0 || index >= strings.Length) { throw new ResolutionSnapshotInvalid("bad string"); }
      return strings[index];
    }
    System.Type TypeAt() {
      var index = Int();
      if (index < 0 || index >= types.Length) { throw new ResolutionSnapshotInvalid("bad type"); }
      return types[index];
    }
    var entryCount = Count(Math.Min(MaxObjects, remaining()));
    var ownPieces = world.Frames.Of(world.Module);
    var ownTokens = new List<Token>();
    var copyAnchors = new List<Token>();
    ResolutionFrames.Piece PieceAt(long index) =>
      index >= 0 && index < ownPieces.Length ? ownPieces[index] : throw new ResolutionSnapshotInvalid("bad piece");
    Value ReadValue() {
      var tag = (Tag)reader.ReadByte();
      switch (tag) {
        case Tag.Null: return new(-1, null);
        case Tag.False: return new(-1, BoxedFalse);
        case Tag.True: return new(-1, BoxedTrue);
        case Tag.Int32: return new(-1, checked((int)Int()));
        case Tag.Int64: return new(-1, Int());
        case Tag.String: return new(-1, Str());
        case Tag.Char: return new(-1, checked((char)Int()));
        case Tag.Double: return new(-1, BitConverter.Int64BitsToDouble(Int()));
        case Tag.Byte: return new(-1, checked((byte)Int()));
        case Tag.SByte: return new(-1, checked((sbyte)Int()));
        case Tag.Int16: return new(-1, checked((short)Int()));
        case Tag.UInt16: return new(-1, checked((ushort)Int()));
        case Tag.UInt32: return new(-1, checked((uint)Int()));
        case Tag.UInt64: return new(-1, unchecked((ulong)Int()));
        case Tag.Single: return new(-1, BitConverter.Int32BitsToSingle(checked((int)Int())));
        case Tag.Decimal:
          return new(-1, new decimal([checked((int)Int()), checked((int)Int()), checked((int)Int()), checked((int)Int())]));
        case Tag.BigInteger: return new(-1, BigInteger.Parse(Str(), System.Globalization.CultureInfo.InvariantCulture));
        case Tag.Uri: {
            var text = Str();
            return new(-1, new Uri(text, Int() == 1 ? UriKind.Absolute : UriKind.RelativeOrAbsolute));
          }
        case Tag.Guid: return new(-1, Guid.ParseExact(Str(), "N"));
        case Tag.Enum: {
            var type = TypeAt();
            if (!type.IsEnum) { throw new ResolutionSnapshotInvalid("not an enum"); }
            return new(-1, Enum.ToObject(type, Int()));
          }
        case Tag.External: {
            var kind = (ResolutionExternal.Kinds)reader.ReadByte();
            var name = Str();
            var index = checked((int)Int());
            var type = TypeAt();
            var bound = world.Object(new ResolutionExternal(kind, name, index));
            if (bound.GetType() != type) { throw new ResolutionSnapshotInvalid("external " + name + "#" + index + " has another type"); }
            return new(-1, bound);
          }
        case Tag.PieceUri: {
            var index = Int();
            return new(-1, PieceAt(index).Start.Uri, (int)index);
          }
        case Tag.AnchorUri: {
            var module = Str();
            var anchor = world.PrerequisiteToken(module, checked((int)Int()));
            copyAnchors.Add(anchor);
            return new(-1, anchor.Uri, -1 - copyAnchors.Count);
          }
        case Tag.SourceToken: {
            Token token;
            switch ((TokenOwner)reader.ReadByte()) {
              case TokenOwner.Own: {
                  var rank = Int();
                  var piece = PieceAt(Int());
                  var offset = checked((int)Int());
                  var val = Str();
                  if (rank == ownTokens.Count) {
                    ownTokens.Add(world.SourceTokens.Find(piece.Start.Uri.ToString(), checked(piece.Start.pos + offset), val));
                  } else if (rank < 0 || rank > ownTokens.Count) {
                    throw new ResolutionSnapshotInvalid("bad token rank");
                  }
                  token = ownTokens[(int)rank];
                  if (token.val != val) { throw new ResolutionSnapshotInvalid("token " + val + " differs"); }
                  break;
                }
              case TokenOwner.Prerequisite: {
                  var module = Str();
                  var rank = checked((int)Int());
                  var val = Str();
                  token = world.PrerequisiteToken(module, rank);
                  if ((token.val ?? "") != val) { throw new ResolutionSnapshotInvalid("token " + val + " of " + module + " differs"); }
                  break;
                }
              default: throw new ResolutionSnapshotInvalid("bad token owner");
            }
            return new(-1, token);
          }
        case Tag.Reference: {
            var id = Int();
            if (id < 0 || id >= entryCount) { throw new ResolutionSnapshotInvalid("bad reference"); }
            return new((int)id, null);
          }
        default: throw new ResolutionSnapshotInvalid("bad tag");
      }
    }
    if (scratch.Entries.Length < entryCount) { scratch.Entries = new Entry[entryCount]; }
    var entries = scratch.Entries;
    scratch.EntryCount = entryCount;
    var vals = scratch.Values;
    void Push(Value value) {
      if (scratch.Used == vals.Length) { Array.Resize(ref vals, vals.Length * 2); scratch.Values = vals; }
      vals[scratch.Used++] = value;
    }
    void ReadValues(int i, EntryKind kind, System.Type type, int n) {
      entries[i] = new Entry { Kind = kind, Type = type, Start = scratch.Used, Count = n };
      for (var j = 0; j < n; j++) { Push(ReadValue()); }
    }
    var objects = new object[entryCount];
    for (var i = 0; i < entryCount; i++) {
      var kind = (EntryKind)reader.ReadByte();
      switch (kind) {
        case EntryKind.Scope: {
            var tokens = new (ResolutionExternal.Kinds, string, int)[Count(remaining())];
            for (var j = 0; j < tokens.Length; j++) {
              var tokenKind = (ResolutionExternal.Kinds)reader.ReadByte();
              tokens[j] = tokenKind == 0 ? (0, "", checked((int)Int())) : (tokenKind, Str(), checked((int)Int()));
            }
            var names = new string[Count(remaining())];
            for (var j = 0; j < names.Length; j++) { names[j] = Str(); }
            entries[i] = new Entry { Kind = kind, Type = typeof(VisibilityScope), Start = scratch.Used, Scope = new(tokens, names) };
            objects[i] = new VisibilityScope();
            break;
          }
        case EntryKind.Array: {
            var type = TypeAt();
            if (!type.IsArray || type.GetArrayRank() != 1) { throw new ResolutionSnapshotInvalid("not an array"); }
            var n = Count(remaining());
            ReadValues(i, kind, type, n);
            objects[i] = Array.CreateInstance(type.GetElementType()!, n);
            break;
          }
        case EntryKind.Collection: {
            var type = TypeAt();
            var comparer = reader.ReadByte();
            var comparerType = comparer == 3 ? TypeAt() : null;
            ReadValues(i, kind, type, Count(remaining()));
            objects[i] = ResolutionGraphSchema.NewCollection(type, comparer, comparerType);
            break;
          }
        case EntryKind.Lock:
          entries[i] = new Entry { Kind = kind, Type = typeof(object), Start = scratch.Used };
          objects[i] = new object();
          break;
        case EntryKind.Fields: {
            var typeIndex = Int();
            if (typeIndex < 0 || typeIndex >= types.Length || !schemas[typeIndex]) { throw new ResolutionSnapshotInvalid("bad fields type"); }
            var type = types[typeIndex];
            ReadValues(i, kind, type, ResolutionGraphSchema.Fields(type).Length);
            objects[i] = RuntimeHelpers.GetUninitializedObject(type);
            break;
          }
        default: throw new ResolutionSnapshotInvalid("bad entry kind");
      }
    }
    var roots = new Value[Count(remaining())];
    for (var i = 0; i < roots.Length; i++) { roots[i] = ReadValue(); }
    var ownedTokenCount = Count(int.MaxValue);
    if (stream.Position != stream.Length) { throw new ResolutionSnapshotInvalid("trailing data"); }
    PipelineProfile.ModulePhase(world.Options, "cache.materialize.read");

    // Fresh tokens, minted in rank order, keep the order of the captured tokens among themselves.
    var ownedTokens = new uint[ownedTokenCount];
    for (var i = 0; i < ownedTokenCount; i++) { ownedTokens[i] = ResolutionGraphSchema.Tokens(new VisibilityScope("snapshot")).Single(); }

    var initialized = new bool[entryCount];
    // A boxed value is copied wherever it is stored, and an immutable list cannot be filled once created,
    // so each is completed before it is first read.
    // Each entry's collection kind and whether it is lazy or hashed, computed once.
    var modes = new int[entryCount];
    for (var i = 0; i < entryCount; i++) {
      var entry = entries[i];
      var collection = entry.Kind == EntryKind.Collection ? (int)(ResolutionGraphSchema.CollectionKind(entry.Type) ?? 0) : 0;
      var lazy = (entry.Kind == EntryKind.Fields && entry.Type.IsValueType) || collection == (int)ResolutionGraphSchema.Collection.ImmutableList;
      var hashed = collection is (int)ResolutionGraphSchema.Collection.Dictionary or (int)ResolutionGraphSchema.Collection.Set or
        (int)ResolutionGraphSchema.Collection.SortedSet;
      modes[i] = collection | (lazy ? LazyBit : 0) | (hashed ? HashedBit : 0);
    }
    bool IsLazy(int id) => (modes[id] & LazyBit) != 0;
    bool IsHashed(int id) => (modes[id] & HashedBit) != 0;
    object ReadReference(int id) {
      if (IsLazy(id) && !initialized[id]) {
        initialized[id] = true;
        Fill(id);
      }
      return objects[id];
    }
    object? Read(Value value) => value.Reference >= 0 ? ReadReference(value.Reference) : value.Data;
    void Fill(int id) {
      var entry = entries[id];
      var target = objects[id];
      switch (entry.Kind) {
        case EntryKind.Fields: {
            var fields = ResolutionGraphSchema.Fields(entry.Type);
            var writers = ResolutionGraphSchema.FieldWriters(entry.Type);
            for (var j = 0; j < fields.Length; j++) {
              var value = Read(vals[entry.Start + j]);
              if (value == null && fields[j].FieldType.IsValueType && Nullable.GetUnderlyingType(fields[j].FieldType) == null) {
                throw new ResolutionSnapshotInvalid("null value field");
              }
              writers[j](target, value);
            }
            if (entry.Type == typeof(Token) && vals[entry.Start + TokenPlace[0]].Piece is var index && index != -1) {
              // An own piece's index, or an anchor's, as -2 - its index among the anchors read.
              var start = index >= 0 ? ownPieces[index].Start : copyAnchors[-2 - index];
              var copy = (Token)target;
              var lines = copy.line;
              copy.pos = copy.pos == NoOffset ? 0 : checked(start.pos + copy.pos);
              copy.line = checked(start.line + lines);
              if (lines == 0) { copy.col = checked(start.col + copy.col); }
            }
            break;
          }
        case EntryKind.Array:
          for (var j = 0; j < entry.Count; j++) { ((Array)target).SetValue(Read(vals[entry.Start + j]), j); }
          break;
        case EntryKind.Scope: {
            var tokens = ResolutionGraphSchema.Tokens((VisibilityScope)target);
            foreach (var (kind, name, rank) in entry.Scope!.Tokens) {
              if (kind == 0) {
                if (rank < 0 || rank >= ownedTokens.Length) { throw new ResolutionSnapshotInvalid("bad scope token"); }
                tokens.Add(ownedTokens[rank]);
              } else {
                tokens.Add(world.Token(new ResolutionExternalToken(kind, name, rank)));
              }
            }
            ResolutionGraphSchema.Ids((VisibilityScope)target).UnionWith(entry.Scope!.Names);
            break;
          }
        case EntryKind.Collection:
          switch ((ResolutionGraphSchema.Collection)(modes[id] & CollectionMask)) {
            case ResolutionGraphSchema.Collection.Dictionary:
              if (entry.Count % 2 != 0) { throw new ResolutionSnapshotInvalid("dictionary arity"); }
              for (var j = entry.Start; j < entry.Start + entry.Count; j += 2) {
                ((IDictionary)target).Add(Read(vals[j]) ?? throw new ResolutionSnapshotInvalid("null key"), Read(vals[j + 1]));
              }
              break;
            case ResolutionGraphSchema.Collection.ImmutableList: {
                var ops = ResolutionGraphSchema.Ops(entry.Type)!;
                for (var j = entry.Start; j < entry.Start + entry.Count; j++) { ops.Add(target, Read(vals[j])); }
                objects[id] = ops.Freeze!(target);
                break;
              }
            case ResolutionGraphSchema.Collection.Stack: {
                var ops = ResolutionGraphSchema.Ops(entry.Type)!;
                for (var j = entry.Start + entry.Count - 1; j >= entry.Start; j--) { ops.Add(target, Read(vals[j])); }
                break;
              }
            default: {
                var ops = ResolutionGraphSchema.Ops(entry.Type)!;
                for (var j = entry.Start; j < entry.Start + entry.Count; j++) { ops.Add(target, Read(vals[j])); }
                break;
              }
          }
          break;
      }
    }
    try {
      // Hash containers are filled last, after every field their keys' hash codes may read.
      for (var i = 0; i < entryCount; i++) {
        if (!IsHashed(i) && !IsLazy(i)) { Fill(i); }
      }
      for (var i = 0; i < entryCount; i++) {
        if (IsLazy(i)) { ReadReference(i); }
      }
      for (var i = 0; i < entryCount; i++) {
        if (IsHashed(i)) { Fill(i); }
      }
    } catch (TargetInvocationException error) {
      throw new ResolutionSnapshotInvalid("materialization failed: " + error.InnerException?.Message);
    } catch (ArgumentException error) {
      throw new ResolutionSnapshotInvalid("materialization failed: " + error.Message);
    } catch (InvalidCastException error) {
      throw new ResolutionSnapshotInvalid("materialization failed: " + error.Message);
    }
    PipelineProfile.ModulePhase(world.Options, "cache.materialize.fill");
    return new(roots.Select(Read).ToArray(), objects, ownedTokens, ownTokens.ToArray());
  }
}
