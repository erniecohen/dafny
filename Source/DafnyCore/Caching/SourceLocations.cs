#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Dafny.Caching;

/// <summary>
/// Source locations as the persistent tables of --persistent-translation record them, so that
/// two checkouts of the same sources, in different directories, share entries, and so that a line
/// inserted above a declaration does not move what is recorded for it.
///
/// A position is recorded relative to the innermost declaration (module, type or member) that
/// encloses it, by that declaration's qualified name, which is unique in the program, and not by
/// its file. A position that no declaration encloses is recorded with its file, which is named by
/// the SHA-256 of its text rather than by its path. In the stored Boogie text, which is never
/// loaded, a source path is replaced by its file name. Report positions use the same declaration anchors to recover current file names and lines;
/// replayed tasks are bound to the current program's symbols.
/// </summary>
internal sealed class SourceLocations {
  private static readonly ConditionalWeakTable<Program, SourceLocations> ForProgram = new();
  private readonly Dictionary<string, string> labels = new(StringComparer.Ordinal);
  private readonly Dictionary<string, Enclosing[]> declarations = new(StringComparer.Ordinal);
  private readonly (string Text, string Label)[] paths;
  private readonly Dictionary<string, Uri?> files = new(StringComparer.Ordinal);
  private readonly Lazy<Dictionary<string, (Uri Uri, Enclosing Span)?>> reportAnchors;
  private readonly Lazy<Dictionary<(string Uri, int Position), Token?>> proofTokens;

  private readonly record struct Enclosing(int Start, int End, int Line, string Name);

  private SourceLocations(Program program) {
    reportAnchors = new Lazy<Dictionary<string, (Uri, Enclosing)?>>(BuildReportAnchors);
    proofTokens = new Lazy<Dictionary<(string, int), Token?>>(() => {
      var result = new Dictionary<(string, int), Token?>();
      var seen = new HashSet<Token>(ReferenceEqualityComparer.Instance);
      var starts = program.Files.Select(f => f.Origin.EntireRangeWithFallback.StartToken)
        .Concat(program.RawModules().SelectMany(m => m.TopLevelDecls)
          .SelectMany(d => d is TopLevelDeclWithMembers members
            ? members.Members.Select(m => m.StartToken).Prepend(d.StartToken) : new[] { d.StartToken }));
      foreach (var start in starts) {
        for (var token = start; token != null && seen.Add(token); token = token.Next) {
          if (token.Uri == null) { continue; }
          var key = (token.Uri.ToString(), token.pos);
          // Parsed files take precedence over generated/cloned tokens at the same source offset.
          // The complete current excerpt is validated before any dependency is restored.
          result.TryAdd(key, token);
        }
      }
      return result;
    });
    foreach (var file in program.Files) {
      var uri = file.Origin.Uri;
      if (uri == null || labels.ContainsKey(uri.ToString())) { continue; }
      var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(file.Origin.EntireRangeWithFallback.PrintOriginal())));
      labels[uri.ToString()] = uri.IsFile ? "source:" + digest : uri.ToString();
    }
    foreach (var uri in program.Files.Select(f => f.Origin.Uri).Where(u => u != null).Distinct()) {
      void Name(string name, Uri value) {
        // A name that two files share names neither.
        files[name] = files.TryGetValue(name, out var known) && known != value ? null : value;
      }
      Name(uri.ToString(), uri);
      if (uri.IsFile) { Name(uri.LocalPath, uri); Name(System.IO.Path.GetFileName(uri.LocalPath), uri); }
    }
    var spans = new Dictionary<string, List<Enclosing>>(StringComparer.Ordinal);
    void Add(IOrigin? origin, string name) {
      var range = origin?.EntireRangeWithFallback;
      var start = range?.StartToken;
      var end = range?.EndToken;
      if (start?.Uri == null || end == null || start.Uri != end.Uri || end.pos < start.pos) { return; }
      var key = start.Uri.ToString();
      if (!spans.TryGetValue(key, out var list)) { spans[key] = list = new List<Enclosing>(); }
      list.Add(new Enclosing(start.pos, end.pos, start.line, name));
    }
    foreach (var module in program.RawModules()) {
      // The default module spans whole files, so it encloses nothing by its name; its own
      // declarations do, so that top-level code, too, is recorded apart from the rest of its file.
      if (!module.IsDefaultModule) { Add(module.Origin, module.FullName); }
      foreach (var declaration in module.TopLevelDecls) {
        if (declaration is not DefaultClassDecl) { Add(declaration.Origin, declaration.FullDafnyName); }
        if (declaration is TopLevelDeclWithMembers withMembers) {
          foreach (var member in withMembers.Members) { Add(member.Origin, member.FullDafnyName); }
        }
      }
    }
    foreach (var (uri, list) in spans) {
      // Later starts are inner; among equal starts, the shorter is inner.
      declarations[uri] = list.OrderBy(e => e.Start).ThenByDescending(e => e.End).ThenBy(e => e.Name, StringComparer.Ordinal).ToArray();
    }
    paths = program.Files.Select(f => f.Origin.Uri).Where(u => u is { IsFile: true }).Distinct()
      .SelectMany(u => new[] { (u!.ToString(), "<source>/" + System.IO.Path.GetFileName(u.LocalPath)),
        (u.LocalPath, "<source>/" + System.IO.Path.GetFileName(u.LocalPath)) })
      .OrderByDescending(p => p.Item1.Length).ThenBy(p => p.Item1, StringComparer.Ordinal).ToArray();
  }

  public static SourceLocations For(Program program) => ForProgram.GetValue(program, p => new SourceLocations(p));

  internal ReportPosition? ProofPosition(Token token) {
    var position = Report(token);
    return position?.Filename == ReportFilename.Literal && token.Uri != null
      ? position with { Literal = token.Uri.ToString() } : position;
  }

  internal Token? ProofToken(ReportPosition position) {
    if (position == null || position.Offset < 0) { return null; }
    string? uri;
    long offset = position.Offset;
    var line = position.Line;
    if (position.Filename == ReportFilename.Literal) {
      if (position.Anchor != "" || position.File != "") { return null; }
      uri = position.Literal;
    } else {
      if (Report(position, null) is not { } report) { return null; }
      line = report.Line;
      if (position.Anchor.Length != 0) {
        if (!reportAnchors.Value.TryGetValue(position.Anchor, out var binding) || binding == null) { return null; }
        uri = binding.Value.Uri.ToString();
        offset += binding.Value.Span.Start;
      } else {
        uri = labels.SingleOrDefault(p => p.Value == position.File).Key;
      }
    }
    return uri != null && offset <= int.MaxValue && proofTokens.Value.TryGetValue((uri, (int)offset), out var token) &&
           token != null && token.line == line && token.col == position.Column ? token : null;
  }

  /// <summary>The file of <paramref name="uri"/> by its content, or the URI itself when it names no file of the program.</summary>
  public string Label(Uri? uri) => uri == null ? "" : labels.TryGetValue(uri.ToString(), out var label) ? label : uri.ToString();

  /// <summary>
  /// The file of the program that a file name, as Boogie's own tokens carry it (a URI, a path or,
  /// when no two files share it, a file name), names, or null.
  /// </summary>
  public Uri? File(string name) => files.TryGetValue(name, out var uri) ? uri : null;

  /// <summary><paramref name="text"/> with each source path of the program replaced by its file name.</summary>
  public string Relabel(string text) {
    foreach (var (path, label) in paths) {
      if (path.Length != 0) { text = text.Replace(path, label, StringComparison.Ordinal); }
    }
    return text;
  }

  // A description containing a source name could derive that name from the current checkout.
  // Leave such reports on the fresh path instead of guessing how to rewrite their prose.
  internal bool PortableReportText(string? text) => text == null ||
    !files.Keys.Any(name => name.Length != 0 && text.Contains(name, StringComparison.Ordinal));

  private Dictionary<string, (Uri, Enclosing)?> BuildReportAnchors() {
    var result = new Dictionary<string, (Uri, Enclosing)?>(StringComparer.Ordinal);
    foreach (var (file, spans) in declarations) {
      foreach (var span in spans) {
        var value = (new Uri(file), span);
        result[span.Name] = result.TryGetValue(span.Name, out var known) && known != value ? null : value;
      }
    }
    return result;
  }

  internal ReportPosition? Report(Microsoft.Boogie.IToken token) {
    // The graph fingerprint deliberately erases Boogie's printing-buffer coordinates.
    // These cannot be restored as report locations under that same identity.
    if (token is not IOrigin && (string.IsNullOrEmpty(token.filename) || token.filename.StartsWith('<'))) { return null; }
    var uri = token is IOrigin origin ? origin.Uri : File(token.filename ?? "");
    if (uri == null || !labels.ContainsKey(uri.ToString())) {
      // The graph fingerprint retains literal coordinates for tokens outside this program.
      return new ReportPosition("", "", token.line, token.col, token.pos, ReportFilename.Literal, token.filename);
    }
    var filename = token.filename == Path.GetFileName(uri.LocalPath) ? ReportFilename.Basename
      : token.filename == uri.LocalPath ? ReportFilename.Path
      : token.filename == uri.ToString() ? ReportFilename.Uri : (ReportFilename?)null;
    if (filename == null) { return null; }
    var (anchor, line, offset) = Relative(uri, token.line, token.pos);
    return new ReportPosition(anchor, anchor.Length == 0 ? Label(uri) : "", line, token.col, offset, filename.Value, null);
  }

  internal VerificationAssertionReport? Report(ReportPosition position, string? description) {
    if (position.Anchor == null || position.File == null) { return null; }
    if (position.Filename == ReportFilename.Literal) {
      return position.Anchor.Length == 0 && position.File.Length == 0
        ? new VerificationAssertionReport(position.Literal, position.Line, position.Column, description) : null;
    }
    Uri? uri;
    var line = position.Line;
    if (position.Anchor.Length != 0) {
      if (!reportAnchors.Value.TryGetValue(position.Anchor, out var binding) || binding == null ||
          position.Line < 0 || position.Offset < 0 || position.Offset > binding.Value.Span.End - binding.Value.Span.Start) {
        return null;
      }
      uri = binding.Value.Uri;
      if (position.Line > int.MaxValue - binding.Value.Span.Line) { return null; }
      line += binding.Value.Span.Line;
    } else {
      var matches = labels.Where(p => p.Value == position.File).Select(p => p.Key).Take(2).ToList();
      if (matches.Count != 1) { return null; }
      uri = new Uri(matches[0]);
    }
    var filename = position.Filename switch {
      ReportFilename.Basename => Path.GetFileName(uri.LocalPath),
      ReportFilename.Path => uri.LocalPath,
      ReportFilename.Uri => uri.ToString(),
      _ => null
    };
    return filename == null ? null : new VerificationAssertionReport(filename, line, position.Column, description);
  }

  /// <summary>
  /// The symbol as the persistent tables bind it: its type, its name, its file by content and its
  /// extent relative to its own start, so that it is found again after lines are inserted above it.
  /// </summary>
  public PersistentModuleCache.SymbolEntry Symbol(ICanVerify symbol) {
    var start = symbol.StartToken;
    var end = symbol.EndToken;
    var (enclosing, line, _) = Relative(start.Uri, start.line, start.pos);
    return new PersistentModuleCache.SymbolEntry(symbol.GetType().FullName ?? symbol.GetType().Name, symbol.FullDafnyName,
      enclosing.Length != 0 ? "declaration:" + enclosing : Label(start.Uri), line, start.col, end.line - start.line, end.col);
  }

  /// <summary>A position as the innermost enclosing declaration's name and the offsets from its start.</summary>
  public (string Name, int Line, int Pos) Relative(Uri? uri, int line, int pos) {
    if (uri == null || !declarations.TryGetValue(uri.ToString(), out var spans)) { return ("", line, pos); }
    int low = 0, high = spans.Length - 1, last = -1;
    while (low <= high) {
      var mid = (low + high) / 2;
      if (spans[mid].Start <= pos) { last = mid; low = mid + 1; } else { high = mid - 1; }
    }
    for (var i = last; i >= 0; i--) {
      if (spans[i].End >= pos) { return (spans[i].Name, line - spans[i].Line, pos - spans[i].Start); }
    }
    return ("", line, pos);
  }
}
