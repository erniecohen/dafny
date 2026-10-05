// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace Microsoft.Dafny;

/// <summary>Checks the state relation independently of the recipe producer.</summary>
internal static class B3RealRoundTripRelation {
  private sealed record Row(Ir.Expression Value, HashSet<string> Free);
  internal sealed class Budget {
    private long remaining;
    internal Budget(long remaining) { this.remaining = remaining; }
    internal void Step(long count = 1) {
      remaining -= count;
      if (remaining < 0) { throw new InvalidDataException("Real relation exceeded its preflight work allowance"); }
    }
  }

  internal static void Validate(Ir.Request original, Ir.Request submitted, B3RealPreparationEvidence evidence,
    IToken token, B3RealPreparationLimits? limits = null, Budget? budget = null) {
    limits ??= new B3RealPreparationLimits(); budget ??= new Budget(limits.MaximumWork);
    // Even a direct helper/control invocation cannot hash caller-owned trees first.
    original = B3RealContextPreparation.CaptureForRelation(original, token);
    submitted = B3RealContextPreparation.CaptureForRelation(submitted, token);
    Ir.ProtocolValidation.ValidateRequest(original); Ir.ProtocolValidation.ValidateRequest(submitted);
    var checker = new Checker(token, limits, budget);
    checker.Require(evidence.ProducerVersion == B3RealContextPreparation.ProducerVersion &&
      evidence.MaskId is { Length: > 0 and <= 4096 } &&
      evidence.Applied == (original.ProgramHash != submitted.ProgramHash) &&
      (!evidence.Applied || evidence.DeclineReason == null) &&
      evidence.OriginalProgramHash == original.ProgramHash && evidence.FinalProgramHash == submitted.ProgramHash &&
      original.RequestId == submitted.RequestId && original.Version == submitted.Version &&
      original.UnitId == submitted.UnitId && original.NormalizerVersion == submitted.NormalizerVersion &&
      original.B3Commit == submitted.B3Commit && original.WorkerFingerprint == submitted.WorkerFingerprint &&
      B3RealContextPreparation.JsonEqual(original.Configuration, submitted.Configuration) &&
      original.Obligations.SequenceEqual(submitted.Obligations), "Real relation changed request/source metadata");
    checker.Require(B3RealContextPreparation.JsonEqual(original.Program.Types, submitted.Program.Types) &&
      B3RealContextPreparation.JsonEqual(original.Program.Functions, submitted.Program.Functions) &&
      B3RealContextPreparation.JsonEqual(original.Program.Axioms, submitted.Program.Axioms) &&
      B3RealContextPreparation.JsonEqual(original.Program.Unit.Variables, submitted.Program.Unit.Variables) &&
      original.Program.Unit.Name == submitted.Program.Unit.Name, "Real relation changed declarations or source axioms");
    checker.Statement(original.Program.Unit.Body, submitted.Program.Unit.Body, new(StringComparer.Ordinal), 0);
  }

  private sealed class Checker {
    private readonly IToken token;
    private readonly B3RealPreparationLimits limits;
    private readonly Budget budget;
    internal Checker(IToken token, B3RealPreparationLimits limits, Budget budget) {
      this.token = token; this.limits = limits; this.budget = budget;
    }
    internal void Require(bool condition, string message) => B3RealContextPreparation.Require(condition, message, token);
    private void Visit(int depth) {
      budget.Step(); Require(depth <= limits.MaximumDepth, "Real relation exceeds its depth bound");
    }

    internal void Statement(Ir.Statement before, Ir.Statement after, Dictionary<string, Row> rows, int depth) {
      Visit(depth);
      switch (before, after) {
        case (Ir.Block a, Ir.Block b):
          Require(a.Statements.Count == b.Statements.Count, "Real relation changed statement order/coverage");
          for (var i = 0; i < a.Statements.Count; i++) { Statement(a.Statements[i], b.Statements[i], rows, depth + 1); }
          break;
        case (Ir.Assign a, Ir.Assign b):
          Require(a.Variable == b.Variable, "Real relation changed an assignment target");
          Require(Expression(a.Value, b.Value, rows, depth + 1), "Real relation changed assignment value");
          Invalidate(rows, new HashSet<string>(StringComparer.Ordinal) { a.Variable });
          if (Remember(b.Value, out var free) && !free.Contains(a.Variable)) {
            Require(rows.Count < limits.MaximumRows, "Real relation row bound exceeded");
            rows.Add(a.Variable, new Row(b.Value, free));
          }
          break;
        case (Ir.Havoc a, Ir.Havoc b):
          Require(a.Variables.SequenceEqual(b.Variables), "Real relation changed havoc order/targets");
          Invalidate(rows, a.Variables.ToHashSet(StringComparer.Ordinal)); break;
        case (Ir.Check a, Ir.Check b):
          Require(a.ObligationId == b.ObligationId && a.Learn == b.Learn && Expression(a.Condition, b.Condition, rows, depth + 1),
            "Real relation changed check identity, learning or condition"); break;
        case (Ir.Assume a, Ir.Assume b):
          Require(Expression(a.Condition, b.Condition, rows, depth + 1), "Real relation changed an assumption"); break;
        case (Ir.Conditional a, Ir.Conditional b):
          Require(Expression(a.Condition, b.Condition, rows, depth + 1), "Real relation changed a branch guard");
          budget.Step(rows.Count * 2L);
          Statement(a.Then, b.Then, new(rows, StringComparer.Ordinal), depth + 1);
          Statement(a.Else, b.Else, new(rows, StringComparer.Ordinal), depth + 1); rows.Clear(); break;
        case (Ir.Choice a, Ir.Choice b):
          Require(a.Branches.Count == b.Branches.Count, "Real relation changed choice alternatives");
          budget.Step(rows.Count * (long)a.Branches.Count);
          for (var i = 0; i < a.Branches.Count; i++) {
            Statement(a.Branches[i], b.Branches[i], new(rows, StringComparer.Ordinal), depth + 1);
          }
          rows.Clear(); break;
        case (Ir.Loop a, Ir.Loop b):
          Require(a.Invariants.Count == 0 && b.Invariants.Count == 0, "Real relation cannot change native loop invariants");
          rows.Clear(); Statement(a.Body, b.Body, new(StringComparer.Ordinal), depth + 1); rows.Clear(); break;
        case (Ir.Labeled a, Ir.Labeled b):
          Require(a.Name == b.Name, "Real relation changed a control label");
          rows.Clear(); Statement(a.Body, b.Body, new(StringComparer.Ordinal), depth + 1); rows.Clear(); break;
        case (Ir.Exit a, Ir.Exit b): Require(a.Label == b.Label, "Real relation changed exit target"); rows.Clear(); break;
        case (Ir.Return, Ir.Return): rows.Clear(); break;
        default: Require(false, "Real relation changed or does not recognize control topology"); break;
      }
    }

    private bool Expression(Ir.Expression before, Ir.Expression after, Dictionary<string, Row> rows, int depth) {
      Visit(depth);
      if (before.Type != after.Type) { return false; }
      if (before is Ir.Variable variable && rows.TryGetValue(variable.Name, out var row)) {
        return Same(before, after) || Same(row.Value, after);
      }
      if (before is Ir.Operation a && a.Operator == Ir.Operator.ToInt && a.Type == "int" && a.Arguments.Count == 1 &&
          EmbeddingOf(a.Arguments[0], after, rows, depth + 1)) { return true; }
      if (before is Ir.Operation x && after is Ir.Operation y && x.Operator == y.Operator &&
          x.Arguments.Count == y.Arguments.Count) {
        for (var i = 0; i < x.Arguments.Count; i++) {
          if (!Expression(x.Arguments[i], y.Arguments[i], rows, depth + 1)) { return false; }
        }
        return true;
      }
      if (before is Ir.Label l && after is Ir.Label r && l.Name == r.Name) {
        return Expression(l.Body, r.Body, rows, depth + 1);
      }
      // Exact preservation at binders, applications, words, and ordinary leaves.
      return Same(before, after);
    }

    private bool EmbeddingOf(Ir.Expression before, Ir.Expression integer, Dictionary<string, Row> rows, int depth) {
      Visit(depth);
      if (before.Type != "real" || integer.Type != "int") { return false; }
      if (before is Ir.Operation { Operator: Ir.Operator.ToReal, Type: "real" } embedding && embedding.Arguments.Count == 1 &&
          embedding.Arguments[0].Type == "int") {
        return Expression(embedding.Arguments[0], integer, rows, depth + 1);
      }
      if (before is Ir.Variable variable && rows.TryGetValue(variable.Name, out var row) &&
          row.Value is Ir.Operation { Operator: Ir.Operator.ToReal, Type: "real" } remembered && remembered.Arguments.Count == 1) {
        return Same(remembered.Arguments[0], integer);
      }
      return false;
    }

    private bool Same(Ir.Expression left, Ir.Expression right) {
      if (ReferenceEquals(left, right)) { budget.Step(); return true; }
      budget.Step(B3RealRoundTrip.ExpressionSize(left).Nodes + B3RealRoundTrip.ExpressionSize(right).Nodes);
      return B3RealContextPreparation.JsonEqual(left, right);
    }
    private void Invalidate(Dictionary<string, Row> rows, HashSet<string> changed) {
      var discarded = new List<string>();
      foreach (var pair in rows) {
        budget.Step(1 + pair.Value.Free.Count);
        if (changed.Contains(pair.Key) || pair.Value.Free.Overlaps(changed)) { discarded.Add(pair.Key); }
      }
      foreach (var key in discarded) { rows.Remove(key); }
    }
    private bool Remember(Ir.Expression expression, out HashSet<string> free) {
      free = new(StringComparer.Ordinal);
      var current = expression;
      while (true) {
        budget.Step();
        switch (current) {
          case Ir.Variable variable when variable.Type is "int" or "real": free.Add(variable.Name); return true;
          case Ir.IntegerLiteral or Ir.RationalLiteral: return true;
          case Ir.Operation { Operator: Ir.Operator.ToReal or Ir.Operator.ToInt } conversion when conversion.Arguments.Count == 1:
            current = conversion.Arguments[0]; break;
          default: return false;
        }
      }
    }
  }
}
