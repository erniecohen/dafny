// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ir = DafnyB3Protocol;

namespace Microsoft.Dafny;

/// <summary>Produces recipes; the relation checker independently licenses submitted changes.</summary>
internal static class B3RealRoundTrip {
  internal sealed class Work {
    internal B3RealPreparationLimits Limits { get; }
    internal long Count { get; private set; }
    private long memberships;
    internal Work(B3RealPreparationLimits limits) { Limits = limits; }
    internal void Step(long count = 1) {
      Count = checked(Count + count);
      if (Count > Limits.MaximumWork) { throw new B3RealOptionalLimit("Real optional work bound exceeded"); }
    }
    internal void Memberships(int count) {
      memberships += count;
      if (memberships > Limits.MaximumFreeMemberships) { throw new B3RealOptionalLimit("Real remembered free-variable bound exceeded"); }
    }
  }
  internal sealed record Size(long Nodes, int Height, long Bytes);
  internal sealed class ExpressionPlan {
    internal Ir.Expression Source { get; }
    // Default means a whole unchanged barrier; an empty array means a leaf.
    internal ImmutableArray<ExpressionPlan> Children { get; }
    internal Size Size { get; }
    private Ir.Expression? value;
    internal ExpressionPlan(Ir.Expression source, ImmutableArray<ExpressionPlan> children, Size size) {
      Source = source; Children = children; Size = size;
    }
    internal Ir.Expression Materialize() {
      if (value != null) { return value; }
      value = Children.IsDefault ? Source : Source switch {
        Ir.Operation operation => operation with { Arguments = Children.Select(child => child.Materialize()).ToImmutableArray() },
        Ir.Label label => label with { Body = Children[0].Materialize() },
        _ => Source
      };
      return value;
    }
  }
  internal sealed class StatementPlan {
    internal Ir.Statement Source { get; }
    internal ExpressionPlan? Expression { get; }
    internal ImmutableArray<StatementPlan> Children { get; }
    internal long Nodes { get; }
    internal int Height { get; }
    internal long Bytes { get; }
    internal StatementPlan(Ir.Statement source, ExpressionPlan? expression, ImmutableArray<StatementPlan> children) {
      Source = source; Expression = expression; Children = children;
      Nodes = checked(1 + (expression?.Size.Nodes ?? 0) + children.Sum(child => child.Nodes));
      Height = Math.Max(expression == null ? 0 : expression.Size.Height + 1,
        children.Length == 0 ? 0 : children.Max(child => child.Height + 1));
      Bytes = checked(512 + (expression?.Size.Bytes ?? 0) + children.Sum(child => child.Bytes) + StatementText(source));
    }
    internal Ir.Statement Materialize() => Source switch {
      Ir.Block => new Ir.Block(Children.Select(child => child.Materialize()).ToImmutableArray()),
      Ir.Assign assign => assign with { Value = Expression!.Materialize() },
      Ir.Check check => check with { Condition = Expression!.Materialize() },
      Ir.Assume assume => assume with { Condition = Expression!.Materialize() },
      Ir.Choice => new Ir.Choice(Children.Select(child => child.Materialize()).ToImmutableArray()),
      Ir.Conditional conditional => conditional with { Condition = Expression!.Materialize(),
        Then = Children[0].Materialize(), Else = Children[1].Materialize() },
      Ir.Loop loop => loop with { Body = Children[0].Materialize() },
      Ir.Labeled labeled => labeled with { Body = Children[0].Materialize() },
      _ => Source
    };
  }
  private sealed record Row(ExpressionPlan Plan, ImmutableHashSet<string> Free);

  internal static StatementPlan Plan(Ir.Statement body, Work work) =>
    Statement(body, new Dictionary<string, Row>(StringComparer.Ordinal), work, 0);

  private static StatementPlan Statement(Ir.Statement statement, Dictionary<string, Row> rows, Work work, int depth) {
    Visit(depth, work);
    ExpressionPlan? expression = null;
    var children = ImmutableArray<StatementPlan>.Empty;
    switch (statement) {
      case Ir.Block block:
        children = block.Statements.Select(child => Statement(child, rows, work, depth + 1)).ToImmutableArray(); break;
      case Ir.Assign assign:
        expression = Expression(assign.Value, rows, work, depth + 1);
        Invalidate(rows, new HashSet<string>(StringComparer.Ordinal) { assign.Variable }, work);
        if (Remember(expression, work, out var free) && !free.Contains(assign.Variable)) {
          if (rows.Count >= work.Limits.MaximumRows) { throw new B3RealOptionalLimit("Real remembered row bound exceeded"); }
          work.Memberships(free.Count); rows.Add(assign.Variable, new Row(expression, free));
        }
        break;
      case Ir.Havoc havoc:
        Invalidate(rows, havoc.Variables.ToHashSet(StringComparer.Ordinal), work); break;
      case Ir.Check check: expression = Expression(check.Condition, rows, work, depth + 1); break;
      case Ir.Assume assume: expression = Expression(assume.Condition, rows, work, depth + 1); break;
      case Ir.Conditional conditional:
        expression = Expression(conditional.Condition, rows, work, depth + 1);
        work.Step(rows.Count * 2L);
        children = ImmutableArray.Create(Statement(conditional.Then, new(rows, StringComparer.Ordinal), work, depth + 1),
          Statement(conditional.Else, new(rows, StringComparer.Ordinal), work, depth + 1)); rows.Clear(); break;
      case Ir.Choice choice:
        work.Step(rows.Count * (long)choice.Branches.Count);
        children = choice.Branches.Select(child => Statement(child, new(rows, StringComparer.Ordinal), work, depth + 1)).ToImmutableArray();
        rows.Clear(); break;
      case Ir.Loop loop:
        rows.Clear();
        if (loop.Invariants.Count != 0) { throw new B3RealOptionalLimit("Native invariant-bearing loops cannot be prepared"); }
        children = ImmutableArray.Create(Statement(loop.Body, new(StringComparer.Ordinal), work, depth + 1)); rows.Clear(); break;
      case Ir.Labeled labeled:
        rows.Clear(); children = ImmutableArray.Create(Statement(labeled.Body, new(StringComparer.Ordinal), work, depth + 1));
        rows.Clear(); break;
      case Ir.Exit or Ir.Return: rows.Clear(); break;
      default: throw new InvalidOperationException("Unknown typed Real statement");
    }
    var result = new StatementPlan(statement, expression, children);
    if (result.Nodes > work.Limits.MaximumOutputNodes || result.Height + depth > work.Limits.MaximumDepth) {
      throw new B3RealOptionalLimit("Real recipe node/depth growth exceeded");
    }
    return result;
  }

  private static ExpressionPlan Expression(Ir.Expression expression, Dictionary<string, Row> rows, Work work, int depth) {
    Visit(depth, work);
    if (expression is Ir.Variable variable && rows.TryGetValue(variable.Name, out var row)) {
      if (row.Plan.Size.Height + depth > work.Limits.MaximumDepth) { throw new B3RealOptionalLimit("Real substitution depth exceeded"); }
      return row.Plan; // Already expanded; never recursively follow the current dictionary.
    }
    if (expression is Ir.Operation operation) {
      var arguments = operation.Arguments.Select(child => Expression(child, rows, work, depth + 1)).ToImmutableArray();
      if (operation.Operator == Ir.Operator.ToInt && operation.Type == "int" && arguments.Length == 1 &&
          arguments[0].Source is Ir.Operation { Operator: Ir.Operator.ToReal, Type: "real" } &&
          !arguments[0].Children.IsDefault && arguments[0].Children.Length == 1 && arguments[0].Children[0].Source.Type == "int") {
        return arguments[0].Children[0];
      }
      return new ExpressionPlan(expression, arguments, Combine(expression, arguments.Select(child => child.Size)));
    }
    if (expression is Ir.Label label) {
      var child = Expression(label.Body, rows, work, depth + 1);
      return new ExpressionPlan(expression, ImmutableArray.Create(child), Combine(expression, new[] { child.Size }));
    }
    // No substitutions inside applications, words, binders or patterns.
    var size = ExpressionSize(expression, work, depth);
    return new ExpressionPlan(expression, default, size);
  }

  private static void Invalidate(Dictionary<string, Row> rows, HashSet<string> changed, Work work) {
    var remove = new List<string>();
    foreach (var pair in rows) {
      work.Step(1 + pair.Value.Free.Count);
      if (changed.Contains(pair.Key) || pair.Value.Free.Overlaps(changed)) { remove.Add(pair.Key); }
    }
    foreach (var key in remove) { rows.Remove(key); }
  }
  private static bool Remember(ExpressionPlan plan, Work work, out ImmutableHashSet<string> free) {
    var names = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
    var current = plan;
    while (true) {
      work.Step();
      switch (current.Source) {
        case Ir.Variable variable when variable.Type is "int" or "real": names.Add(variable.Name); free = names.ToImmutable(); return true;
        case Ir.IntegerLiteral or Ir.RationalLiteral: free = names.ToImmutable(); return true;
        case Ir.Operation { Operator: Ir.Operator.ToInt or Ir.Operator.ToReal } when !current.Children.IsDefault && current.Children.Length == 1:
          current = current.Children[0]; break;
        default: free = ImmutableHashSet<string>.Empty; return false;
      }
    }
  }

  internal static Size ExpressionSize(Ir.Expression expression, Work? work = null, int depth = 0) {
    if (work != null) { Visit(depth, work); }
    if (depth > Ir.Protocol.MaximumDepth) { throw new B3RealOptionalLimit("Real expression size depth exceeded"); }
    IEnumerable<Ir.Expression> children = expression switch {
      Ir.Application application => application.Arguments,
      Ir.Operation operation => operation.Arguments,
      Ir.BitvectorOperation word => word.Arguments,
      Ir.Quantifier quantifier => new[] { quantifier.Body }.Concat(quantifier.Patterns.SelectMany(pattern => pattern)),
      Ir.Let let => new[] { let.Value, let.Body },
      Ir.Label label => new[] { label.Body },
      Ir.BooleanLiteral or Ir.IntegerLiteral or Ir.RationalLiteral or Ir.BitvectorLiteral or Ir.Variable => Array.Empty<Ir.Expression>(),
      _ => throw new InvalidOperationException("Unknown typed Real expression size")
    };
    var result = Combine(expression, children.Select(child => ExpressionSize(child, work, depth + 1)));
    var bindings = expression is Ir.Quantifier q ? q.Bindings : expression is Ir.Let l ? new[] { l.Binding } : Array.Empty<Ir.Binding>();
    foreach (var binding in bindings) {
      work?.Step(); result = result with { Nodes = result.Nodes + 1, Height = Math.Max(result.Height, 1),
        Bytes = result.Bytes + 512 + Text(binding.Name) + Text(binding.Type) };
    }
    return result;
  }
  private static Size Combine(Ir.Expression expression, IEnumerable<Size> childSizes) {
    long nodes = 1, bytes = 512 + Text(expression.Type); var height = 0;
    foreach (var child in childSizes) {
      nodes = checked(nodes + child.Nodes); bytes = checked(bytes + child.Bytes); height = Math.Max(height, child.Height + 1);
    }
    bytes += expression switch {
      Ir.IntegerLiteral integer => Text(integer.Value),
      Ir.RationalLiteral rational => Text(rational.Numerator) + Text(rational.Denominator),
      Ir.BitvectorLiteral word => Text(word.Value),
      Ir.Variable variable => Text(variable.Name) + Text(variable.Type),
      Ir.Application application => Text(application.Name) + Text(application.Type),
      Ir.Operation operation => Text(operation.Type),
      Ir.BitvectorOperation word => Text(word.Type),
      Ir.Label label => Text(label.Name),
      Ir.Let let => Text(let.Binding.Name) + Text(let.Binding.Type),
      _ => 0
    };
    return new Size(nodes, height, bytes);
  }
  private static long StatementText(Ir.Statement statement) => statement switch {
    Ir.Assign assign => Text(assign.Variable), Ir.Check check => Text(check.ObligationId),
    Ir.Havoc havoc => havoc.Variables.Sum(Text), Ir.Labeled labeled => Text(labeled.Name), Ir.Exit exit => Text(exit.Label), _ => 0
  };
  private static long Text(string text) => checked(6L * text.Length + 2);
  private static void Visit(int depth, Work work) {
    work.Step(); if (depth > work.Limits.MaximumDepth) { throw new B3RealOptionalLimit("Real optional traversal depth exceeded"); }
  }
}
