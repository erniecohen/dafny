using System.Text;

namespace DafnyB3.Solver;

/// <summary>An SMT-LIB 2.6 S-expression. Atoms keep their exact spelling; nothing is case folded.</summary>
public abstract record SExpr {
  public abstract void Write(StringBuilder output);

  public sealed override string ToString() {
    var output = new StringBuilder();
    Write(output);
    return output.ToString();
  }
}

/// <summary>A simple symbol, keyword, numeral, decimal, hexadecimal or binary literal.</summary>
public sealed record SAtom(string Text) : SExpr {
  public override void Write(StringBuilder output) => output.Append(Text);
}

/// <summary>A <c>|quoted symbol|</c>. It is never equal to an atom, even one with the same text.</summary>
public sealed record SQuoted(string Text) : SExpr {
  public override void Write(StringBuilder output) => output.Append('|').Append(Text).Append('|');
}

/// <summary>A string literal, decoded: a doubled quote stands for one quote.</summary>
public sealed record SString(string Text) : SExpr {
  public override void Write(StringBuilder output) => output.Append('"').Append(Text.Replace("\"", "\"\"")).Append('"');
}

public sealed record SList(IReadOnlyList<SExpr> Items) : SExpr {
  public override void Write(StringBuilder output) {
    output.Append('(');
    for (var i = 0; i < Items.Count; i++) {
      if (i != 0) { output.Append(' '); }
      Items[i].Write(output);
    }
    output.Append(')');
  }

  // Structural equality, so that a record comparison never falls back to reference equality.
  public bool Equals(SList? other) => other != null && Items.SequenceEqual(other.Items);
  public override int GetHashCode() => Items.Aggregate(17, (hash, item) => hash * 31 + item.GetHashCode());
}

public sealed class SmtSyntaxException(string message) : IOException(message);

/// <summary>
/// Reads one complete S-expression at a time from a byte stream, within a byte and nesting bound.
/// Strings (with doubled-quote escapes), quoted symbols and line comments are lexed exactly, so a
/// parenthesis or quote inside any of them never changes the structure. End of input is legal only
/// between expressions. Control characters outside whitespace and invalid UTF-8 are rejected.
/// </summary>
public sealed class SmtReader {
  public const int DefaultMaxBytes = 1 << 20;
  public const int DefaultMaxDepth = 64;

  private readonly Stream stream;
  private readonly int maxBytes;
  private readonly int maxDepth;
  private readonly byte[] buffer = new byte[4096];
  private int position;
  private int length;
  private int consumed;
  private static readonly UTF8Encoding Utf8 = new(false, true);

  public SmtReader(Stream stream, int maxBytes = DefaultMaxBytes, int maxDepth = DefaultMaxDepth) {
    if (maxBytes <= 0 || maxDepth <= 0) { throw new ArgumentOutOfRangeException(nameof(maxBytes)); }
    this.stream = stream;
    this.maxBytes = maxBytes;
    this.maxDepth = maxDepth;
  }

  /// <summary>Parses exactly one expression from text, with nothing but whitespace and comments after it.</summary>
  public static SExpr ParseSingle(string text, int maxBytes = DefaultMaxBytes, int maxDepth = DefaultMaxDepth) {
    var reader = new SmtReader(new MemoryStream(Utf8.GetBytes(text)), maxBytes, maxDepth);
    var first = reader.ReadAsync(CancellationToken.None).GetAwaiter().GetResult()
      ?? throw new SmtSyntaxException("Expected one S-expression");
    if (reader.ReadAsync(CancellationToken.None).GetAwaiter().GetResult() != null) {
      throw new SmtSyntaxException("Expected exactly one S-expression");
    }
    return first;
  }

  /// <summary>Returns null on end of input before any token; every per-call byte is counted.</summary>
  public async Task<SExpr?> ReadAsync(CancellationToken cancellationToken) {
    consumed = 0;
    if (!await SkipLayoutAsync(cancellationToken)) { return null; }
    return await ReadExprAsync(0, cancellationToken);
  }

  private async Task<int> PeekAsync(CancellationToken cancellationToken) {
    if (position == length) {
      length = await stream.ReadAsync(buffer, cancellationToken);
      position = 0;
      if (length == 0) { return -1; }
    }
    return buffer[position];
  }

  private async Task<int> NextAsync(CancellationToken cancellationToken) {
    var next = await PeekAsync(cancellationToken);
    if (next < 0) { return next; }
    position++;
    if (++consumed > maxBytes) { throw new SmtSyntaxException("SMT response exceeds limit"); }
    return next;
  }

  private static bool Whitespace(int c) => c is ' ' or '\t' or '\n' or '\r';

  // Skips whitespace and comments. Returns false at end of input.
  private async Task<bool> SkipLayoutAsync(CancellationToken cancellationToken) {
    while (true) {
      var c = await PeekAsync(cancellationToken);
      if (c < 0) { return false; }
      if (Whitespace(c)) { await NextAsync(cancellationToken); continue; }
      if (c != ';') { return true; }
      while (true) {
        var d = await NextAsync(cancellationToken);
        if (d < 0) { return false; }
        if (d == '\n') { break; }
        if (d < 0x20 && d != '\t' && d != '\r') { throw new SmtSyntaxException("Control character in comment"); }
      }
    }
  }

  private async Task<SExpr> ReadExprAsync(int depth, CancellationToken cancellationToken) {
    var c = await NextAsync(cancellationToken);
    switch (c) {
      case < 0: throw new SmtSyntaxException("Truncated S-expression");
      case '(': {
        if (depth + 1 > maxDepth) { throw new SmtSyntaxException("S-expression nesting exceeds limit"); }
        var items = new List<SExpr>();
        while (true) {
          if (!await SkipLayoutAsync(cancellationToken)) { throw new SmtSyntaxException("Truncated S-expression"); }
          if (await PeekAsync(cancellationToken) == ')') {
            await NextAsync(cancellationToken);
            return new SList(items);
          }
          items.Add(await ReadExprAsync(depth + 1, cancellationToken));
        }
      }
      case ')': throw new SmtSyntaxException("Unbalanced closing parenthesis");
      case '"': {
        var text = new MemoryStream();
        while (true) {
          var d = await NextAsync(cancellationToken);
          if (d < 0) { throw new SmtSyntaxException("Truncated string literal"); }
          if (d == '"') {
            if (await PeekAsync(cancellationToken) != '"') { break; }
            await NextAsync(cancellationToken);
          } else if (d < 0x20 && !Whitespace(d)) {
            throw new SmtSyntaxException("Control character in string literal");
          }
          text.WriteByte((byte)d);
        }
        var result = new SString(Decode(text));
        await RequireDelimiterAsync(cancellationToken);
        return result;
      }
      case '|': {
        var text = new MemoryStream();
        while (true) {
          var d = await NextAsync(cancellationToken);
          if (d < 0) { throw new SmtSyntaxException("Truncated quoted symbol"); }
          if (d == '|') { break; }
          if (d == '\\' || (d < 0x20 && !Whitespace(d))) {
            throw new SmtSyntaxException("Invalid character in quoted symbol");
          }
          text.WriteByte((byte)d);
        }
        var result = new SQuoted(Decode(text));
        await RequireDelimiterAsync(cancellationToken);
        return result;
      }
      default: {
        if (!AtomCharacter(c)) { throw new SmtSyntaxException("Invalid SMT character"); }
        var text = new StringBuilder().Append((char)c);
        while (await PeekAsync(cancellationToken) is var d && d >= 0 && AtomCharacter(d)) {
          text.Append((char)await NextAsync(cancellationToken));
        }
        await RequireDelimiterAsync(cancellationToken);
        return new SAtom(text.ToString());
      }
    }
  }

  // A token ends at layout, a parenthesis or end of input; "a|b|" and "x\"y\"" are malformed.
  private async Task RequireDelimiterAsync(CancellationToken cancellationToken) {
    var next = await PeekAsync(cancellationToken);
    if (next >= 0 && !Whitespace(next) && next is not '(' and not ')' and not ';') {
      throw new SmtSyntaxException("Adjacent SMT tokens");
    }
  }

  private static bool AtomCharacter(int c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' ||
    "~!@$%^&*_-+=<>.?/:#".Contains((char)c);

  private static string Decode(MemoryStream bytes) {
    try { return Utf8.GetString(bytes.ToArray()); }
    catch (DecoderFallbackException) { throw new SmtSyntaxException("Invalid UTF-8 in SMT response"); }
  }
}
