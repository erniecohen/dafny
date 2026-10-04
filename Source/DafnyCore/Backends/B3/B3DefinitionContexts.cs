// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ir = DafnyB3Protocol;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny;

public sealed record B3DefinitionOrigin(string Id, string Owner, int AxiomOrdinal, string FormulaHash, string Instance,
  int Line = 0, int Column = 0);
public sealed record B3VerificationContext(string MaskId, Ir.Program Program,
  IReadOnlyList<Ir.SourceIdentity> Obligations, IReadOnlyList<B3DefinitionOrigin> Definitions);

/// <summary>Partitions original checks without changing expressions, state or learning order.</summary>
public static class B3DefinitionContexts {
  public const int MaximumContexts = 16;
  public const int MaximumAggregateNodes = 200000;
  public const int MaximumAggregateBytes = 64 * 1024 * 1024;
  public sealed record Formula(B3DefinitionOrigin Origin, Ir.Expression Condition);

  public static IReadOnlyList<B3VerificationContext> Create(Ir.Program original,
    IReadOnlyList<Ir.SourceIdentity> obligations,
    IReadOnlyDictionary<string, IReadOnlyList<Formula>> selectedDefinitions, Bpl.IToken token) {
    Require(original.Axioms.Count == 0, "Original normalization must precede source definition selection", token);
    var originalNodes = CountNodes(original.Unit.Body, token) + original.Functions.Count + original.Types.Count +
      original.Unit.Variables.Count + obligations.Count;
    Require(originalNodes <= Ir.Protocol.MaximumNodes, "Original context exceeds its node bound", token);
    var baseBytes = JsonBytes(original, Ir.Protocol.MaximumMessageBytes, token);
    var identityBytes = JsonBytes(obligations, Ir.Protocol.MaximumMessageBytes, token);
    Require(baseBytes + identityBytes + 16384 <= Ir.Protocol.MaximumMessageBytes,
      "Original context and source identities exceed the worker request bound", token);
    Require(selectedDefinitions.Count <= Ir.Protocol.MaximumNodes && selectedDefinitions.Values.All(formulas => formulas.Count <= 64),
      "Definition selection exceeds its catalogue bound", token);
    var ids = obligations.Select(identity => identity.Id).ToHashSet(StringComparer.Ordinal);
    Require(ids.Count == obligations.Count && selectedDefinitions.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(ids),
      "Definition context selection differs from the original obligation inventory", token);
    var groups = obligations.GroupBy(identity => string.Join(";", selectedDefinitions[identity.Id]
      .Select(formula => formula.Origin.Id).OrderBy(id => id, StringComparer.Ordinal)), StringComparer.Ordinal).ToArray();
    Require(groups.Length <= MaximumContexts, "Definition mask count exceeds the context bound", token);
    if (groups.Length == 0) {
      return new[] { new B3VerificationContext(Symbol("empty"), original, obligations, Array.Empty<B3DefinitionOrigin>()) };
    }
    var allFormulas = selectedDefinitions.Values.SelectMany(formulas => formulas)
      .DistinctBy(formula => formula.Origin.Id).ToArray();
    var formulaNodes = allFormulas.Sum(formula => CountNodes(formula.Condition, token) + 1L);
    Require((originalNodes + formulaNodes) * groups.Length <= MaximumAggregateNodes,
      "Definition context replay exceeds its aggregate node bound", token);
    // Count exact source JSON without allocating a potentially oversized byte array.
    // Replacing a Check with Assume/empty Block is conservatively covered by the per-check reserve.
    var formulaBytes = allFormulas.Sum(formula => JsonBytes(formula.Condition, Ir.Protocol.MaximumMessageBytes, token) + 256L);
    Require((baseBytes + identityBytes + formulaBytes + obligations.Count * 128L + 16384) * groups.Length <= MaximumAggregateBytes,
      "Definition context replay exceeds its aggregate byte bound", token);
    var contexts = new List<B3VerificationContext>();
    foreach (var group in groups.OrderBy(group => group.Key, StringComparer.Ordinal)) {
      var selected = group.Select(identity => identity.Id).ToHashSet(StringComparer.Ordinal);
      var formulas = selectedDefinitions[group.First().Id].OrderBy(formula => formula.Origin.Id, StringComparer.Ordinal).ToArray();
      var program = original with {
        Axioms = formulas.Select(formula => new Ir.Axiom(Array.Empty<string>(), formula.Condition)).ToArray(),
        Unit = original.Unit with { Body = Replay(original.Unit.Body, selected) }
      };
      Require(JsonBytes(program, Ir.Protocol.MaximumMessageBytes, token) + JsonBytes(group.ToArray(), Ir.Protocol.MaximumMessageBytes, token) + 16384 <= Ir.Protocol.MaximumMessageBytes,
        "A definition context exceeds the worker message bound", token);
      contexts.Add(new B3VerificationContext(Symbol(group.Key), program, group.ToArray(),
        formulas.Select(formula => formula.Origin).ToArray()));
    }
    ValidatePartition(original, obligations, contexts, token);
    return contexts.ToArray();
  }

  public static void ValidatePartition(Ir.Program original, IReadOnlyList<Ir.SourceIdentity> obligations,
    IReadOnlyList<B3VerificationContext> contexts, Bpl.IToken token) {
    Require(contexts.Count is > 0 and <= MaximumContexts &&
      contexts.Select(context => context.MaskId).Distinct(StringComparer.Ordinal).Count() == contexts.Count,
      "Definition context identity inventory is invalid", token);
    _ = CountNodes(original.Unit.Body, token);
    var originalChecks = Checks(original.Unit.Body).ToArray();
    var originalIds = obligations.Select(identity => identity.Id).ToHashSet(StringComparer.Ordinal);
    Require(originalChecks.Length == originalIds.Count && originalChecks.Select(check => check.ObligationId).ToHashSet(StringComparer.Ordinal).SetEquals(originalIds),
      "Original normalized checks differ from their static inventory", token);
    var assigned = new HashSet<string>(StringComparer.Ordinal);
    foreach (var context in contexts) {
      var selected = context.Obligations.Select(identity => identity.Id).ToHashSet(StringComparer.Ordinal);
      Require(context.Program.Unit.Name == original.Unit.Name && context.Program.Types.SequenceEqual(original.Types) &&
        context.Program.Functions.SequenceEqual(original.Functions) && context.Program.Unit.Variables.SequenceEqual(original.Unit.Variables) &&
        context.Obligations.All(obligations.Contains) &&
        selected.Count == context.Obligations.Count && selected.All(assigned.Add),
        "Definition contexts duplicate or replace an original source identity", token);
      _ = CountNodes(context.Program.Unit.Body, token);
      Require(FormulaHashBody(context.Program.Unit.Body, token) == FormulaHashBody(Replay(original.Unit.Body, selected), token),
        "Definition replay changed ordinary state, control, conditions or learning", token);
      var checks = Checks(context.Program.Unit.Body).ToArray();
      Require(checks.Length == selected.Count && checks.Select(check => check.ObligationId).ToHashSet(StringComparer.Ordinal).SetEquals(selected),
        "Selected definition context checks differ from their static inventory", token);
      Require(checks.All(check => originalChecks.Contains(check)),
        "Definition replay changed an original selected check", token);
    }
    Require(assigned.SetEquals(originalIds), "Definition contexts omitted original obligations", token);
  }

  private static Ir.Statement Replay(Ir.Statement statement, HashSet<string> selected) => statement switch {
    Ir.Check check when !selected.Contains(check.ObligationId) => check.Learn ?
      new Ir.Assume(check.Condition) : new Ir.Block(Array.Empty<Ir.Statement>()),
    Ir.Block block => new Ir.Block(block.Statements.Select(child => Replay(child, selected)).ToArray()),
    Ir.Choice choice => new Ir.Choice(choice.Branches.Select(child => Replay(child, selected)).ToArray()),
    Ir.Conditional conditional => conditional with { Then = Replay(conditional.Then, selected), Else = Replay(conditional.Else, selected) },
    Ir.Loop loop => loop with { Body = Replay(loop.Body, selected) },
    Ir.Labeled labeled => labeled with { Body = Replay(labeled.Body, selected) },
    _ => statement
  };
  private static IEnumerable<Ir.Check> Checks(Ir.Statement root) {
    var pending = new Stack<Ir.Statement>(); pending.Push(root);
    while (pending.Count > 0) {
      var statement = pending.Pop();
      if (statement is Ir.Check check) { yield return check; }
      foreach (var child in Children(statement).OfType<Ir.Statement>()) { pending.Push(child); }
    }
  }
  private static long CountNodes(object root, Bpl.IToken token) {
    long count = 0; var pending = new Stack<(object Node, int Depth)>(); pending.Push((root, 0));
    while (pending.Count > 0) {
      var (node, depth) = pending.Pop();
      Require(depth <= Ir.Protocol.MaximumDepth && ++count <= Ir.Protocol.MaximumNodes,
        "Context traversal exceeds its resource bound", token);
      foreach (var child in Children(node)) { pending.Push((child, depth + 1)); }
    }
    return count;
  }
  private static IEnumerable<object> Children(object node) => node switch {
    Ir.Block block => block.Statements, Ir.Choice choice => choice.Branches,
    Ir.Conditional conditional => new object[] { conditional.Condition, conditional.Then, conditional.Else },
    Ir.Loop loop => new object[] { loop.Body }, Ir.Labeled labeled => new object[] { labeled.Body },
    Ir.Assign assign => new object[] { assign.Value }, Ir.Check check => new object[] { check.Condition },
    Ir.Assume assume => new object[] { assume.Condition }, Ir.Application application => application.Arguments,
    Ir.Operation operation => operation.Arguments,
    Ir.Quantifier quantifier => new object[] { quantifier.Body }.Concat(quantifier.Patterns.SelectMany(pattern => pattern)),
    Ir.Let let => new object[] { let.Value, let.Body }, Ir.Label label => new object[] { label.Body },
    _ => Array.Empty<object>()
  };
  private static string FormulaHashBody(Ir.Statement body, Bpl.IToken token) {
    _ = JsonBytes(body, Ir.Protocol.MaximumMessageBytes, token);
    return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(body, Ir.Protocol.JsonOptions))).ToLowerInvariant();
  }
  public static string FormulaHash(Ir.Expression expression) {
    _ = CountNodes(expression, Bpl.Token.NoToken);
    _ = JsonBytes(expression, Ir.Protocol.MaximumMessageBytes, Bpl.Token.NoToken);
    return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(expression, Ir.Protocol.JsonOptions))).ToLowerInvariant();
  }
  private static string Symbol(string text) => "s" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
  private static long JsonBytes<T>(T value, int bound, Bpl.IToken token) {
    using var stream = new CountingStream(bound, token);
    JsonSerializer.Serialize(stream, value, Ir.Protocol.JsonOptions); return stream.Count;
  }
  private sealed class CountingStream(int bound, Bpl.IToken token) : Stream {
    public long Count { get; private set; }
    public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
    public override long Length => Count; public override long Position { get => Count; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) { Count += count; Require(Count <= bound, "Definition JSON exceeds its byte bound", token); }
    public override void Write(ReadOnlySpan<byte> buffer) { Count += buffer.Length; Require(Count <= bound, "Definition JSON exceeds its byte bound", token); }
  }
  private static void Require(bool condition, string message, Bpl.IToken token) {
    if (!condition) { throw new B3DefinitionVisibility.Rejection(message, token); }
  }
}
