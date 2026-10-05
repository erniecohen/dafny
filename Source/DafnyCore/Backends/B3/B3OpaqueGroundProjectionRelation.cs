// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace Microsoft.Dafny;

/// <summary>Recomputes the closed fragment and checks the projection without producer recipes.</summary>
internal static class B3OpaqueGroundProjectionRelation {
  internal sealed class Budget {
    private long remaining;
    internal Budget(long remaining = 800000) { this.remaining = remaining; }
    internal void Step(long count = 1) {
      remaining = checked(remaining - count);
      if (remaining < 0) { throw new InvalidDataException("Opaque relation exceeded its reserved allowance"); }
    }
  }
  internal static void Validate(Ir.Request before, Ir.Request after, B3OpaqueGroundEvidence evidence,
    string expectedMaskId, ImmutableArray<B3DefinitionOrigin> definitions, IToken token, Budget? budget = null) {
    budget ??= new();
    // Caller-owned/forged controls receive bounded pair capture before any hash.
    // This pair is local to one invocation; no all-mask copied inventory is retained.
    before = B3RealContextPreparation.CaptureForRelation(before, token);
    after = B3RealContextPreparation.CaptureForRelation(after, token);
    var left = B3OpaqueGroundProjection.AuditOwned(before, token);
    var right = B3OpaqueGroundProjection.AuditOwned(after, token);
    budget.Step(left.Nodes + right.Nodes);
    Ir.ProtocolValidation.ValidateRequest(before); Ir.ProtocolValidation.ValidateRequest(after);
    var checker = new Checker(before.Program, token, budget);
    checker.Require(!definitions.IsDefault && definitions.Length <= 64 &&
      expectedMaskId is { Length: > 0 and <= 4096 } && evidence != null &&
      evidence.ProducerVersion == B3OpaqueGroundProjection.ProducerVersion && evidence.MaskId == expectedMaskId &&
      evidence.InputProgramHash == before.ProgramHash && evidence.FinalProgramHash == after.ProgramHash &&
      evidence.Applied == (before.ProgramHash != after.ProgramHash) && (!evidence.Applied || evidence.DeclineReason == null),
      "Opaque relation evidence/hash/mask association differs");
    checker.Require(before.Version == after.Version && before.RequestId == after.RequestId && before.UnitId == after.UnitId &&
      before.NormalizerVersion == after.NormalizerVersion && before.B3Commit == after.B3Commit &&
      before.WorkerFingerprint == after.WorkerFingerprint && before.Obligations.SequenceEqual(after.Obligations) &&
      B3RealContextPreparation.JsonEqual(before.Configuration, after.Configuration), "Opaque relation changed source/request metadata");
    if (!evidence.Applied) {
      checker.Require(B3RealContextPreparation.JsonEqual(before, after), "Opaque fallback is not the complete unchanged request"); return;
    }
    checker.Require(definitions.Length == 0 && before.Program.Axioms.Count == 0 && after.Program.Axioms.Count == 0 &&
      after.Program.Types.Count == 0 && after.Program.Functions.Count == 0 && before.Program.Unit.Name == after.Program.Unit.Name,
      "Opaque relation changed definitions/declarations outside its license");
    checker.Declarations(after.Program.Unit.Variables);
    checker.Statement(before.Program.Unit.Body, after.Program.Unit.Body);
    checker.Require(checker.Returned, "Opaque relation needs one terminal Return");
  }

  private sealed class Checker {
    private readonly Ir.Program original;
    private readonly IToken token;
    private readonly Budget budget;
    private readonly HashSet<string> opaque = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Ir.Function> declarations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> bindings = new(StringComparer.Ordinal);
    private readonly HashSet<string> initialized = new(StringComparer.Ordinal);
    internal bool Returned { get; private set; }
    internal Checker(Ir.Program original, IToken token, Budget budget) {
      this.original = original; this.token = token; this.budget = budget;
    }
    internal void Require([DoesNotReturnIf(false)] bool condition, string message) => B3RealContextPreparation.Require(condition, message, token);
    internal void Declarations(IReadOnlyList<Ir.Binding> target) {
      foreach (var type in original.Types) { budget.Step(); opaque.Add(type); }
      foreach (var function in original.Functions) {
        budget.Step(); Require(function.ResultType == "bool" || opaque.Contains(function.ResultType), "Opaque relation function result is coupled");
        foreach (var parameter in function.Parameters) {
          budget.Step(); Require(opaque.Contains(parameter.Type), "Opaque relation function input is coupled");
        }
        declarations.Add(function.Name, function);
      }
      var retained = 0;
      foreach (var binding in original.Unit.Variables) {
        budget.Step(); Require(binding.Type is "bool" or "int" or "real" || opaque.Contains(binding.Type), "Opaque relation contains a primitive word");
        bindings.Add(binding.Name, binding.Type);
        if (binding.Type is "int" or "real") {
          Require(retained < target.Count && target[retained++] == binding, "Opaque relation changed retained binding/order");
        }
      }
      Require(retained == target.Count, "Opaque relation added unit bindings");
    }
    internal void Statement(Ir.Statement before, Ir.Statement after) {
      budget.Step();
      if (before is Ir.Block block) {
        Require(after is Ir.Block, "Opaque relation changed a Block slot");
        var target = (Ir.Block)after;
        Require(block.Statements.Count == target.Statements.Count, "Opaque relation changed child count/order");
        for (var i = 0; i < block.Statements.Count; i++) { Statement(block.Statements[i], target.Statements[i]); }
        return;
      }
      Require(!Returned, "Opaque relation has a statement following Return");
      switch (before) {
        case Ir.Return: Require(after is Ir.Return, "Opaque relation changed Return"); Returned = true; break;
        case Ir.Assign assign:
          if (bindings[assign.Variable] is "int" or "real") {
            Require(after is Ir.Assign target && assign.Variable == target.Variable && SameNumeric(assign.Value, target.Value),
              "Opaque relation changed numeric assignment");
          } else {
            if (bindings[assign.Variable] == "bool") {
              Require(assign.Value is Ir.BooleanLiteral || assign.Value is Ir.Variable copy && initialized.Contains(copy.Name),
                "Opaque relation cannot execute removed Boolean assignment");
              budget.Step(); initialized.Add(assign.Variable);
            } else { Ground(assign.Value); }
            Require(after is Ir.Block { Statements.Count: 0 }, "Opaque relation changed removed assignment slot");
          }
          break;
        case Ir.Check check:
          Require(after is Ir.Check targetCheck && check.ObligationId == targetCheck.ObligationId && check.Learn == targetCheck.Learn &&
            SameNumeric(check.Condition, targetCheck.Condition), "Opaque relation changed check/learning/condition"); break;
        case Ir.Assume assume:
          Require(after is Ir.Assume, "Opaque relation changed an Assume slot");
          Assumption(assume.Condition, ((Ir.Assume)after).Condition); break;
        default: Require(false, "Opaque relation encountered unsupported control"); break;
      }
    }
    private void Assumption(Ir.Expression before, Ir.Expression after) {
      budget.Step();
      if (before is Ir.Operation { Operator: Ir.Operator.And, Arguments.Count: 2 } conjunction) {
        Require(after is Ir.Operation { Operator: Ir.Operator.And, Arguments.Count: 2 } &&
          after.Type == conjunction.Type && ((Ir.Operation)after).ResultType == conjunction.ResultType,
          "Opaque relation changed conjunction topology");
        var target = (Ir.Operation)after;
        Assumption(conjunction.Arguments[0], target.Arguments[0]); Assumption(conjunction.Arguments[1], target.Arguments[1]); return;
      }
      if (before is Ir.Operation { Operator: Ir.Operator.Equal, Arguments.Count: 2 } equality &&
          opaque.Contains(equality.Arguments[0].Type) && equality.Arguments[0].Type == equality.Arguments[1].Type) {
        Ground(equality.Arguments[0]); Ground(equality.Arguments[1]); True(after); return;
      }
      if (before is Ir.Application application && application.Type == "bool") {
        budget.Step(); Require(declarations.TryGetValue(application.Name, out var declaration) && declaration.ResultType == "bool",
          "Opaque relation lacks predicate declaration");
        foreach (var argument in application.Arguments) { Ground(argument); }
        True(after); return;
      }
      // This includes literal false. It can never be justified by singleton erasure.
      Require(SameNumeric(before, after), "Opaque relation changed a retained assumption");
    }
    private void True(Ir.Expression expression) => Require(expression is Ir.BooleanLiteral { Value: true, Type: "bool" },
      "Opaque erased premise is not exactly true");
    private void Ground(Ir.Expression expression) {
      budget.Step(); Require(opaque.Contains(expression.Type), "Opaque relation ground term has nonopaque type");
      switch (expression) {
        case Ir.Variable variable: Require(bindings[variable.Name] == variable.Type, "Opaque relation ground variable differs"); break;
        case Ir.Application application:
          Require(declarations.TryGetValue(application.Name, out var declaration) && declaration.ResultType == application.Type,
            "Opaque relation ground function differs");
          foreach (var argument in application.Arguments) { Ground(argument); } break;
        default: Require(false, "Opaque relation ground term contains unsupported structure"); break;
      }
    }
    // Independent lockstep traversal: no recipe, producer classifier, assumed
    // truth, assignment propagation or serialization at every ancestor is used.
    private bool SameNumeric(Ir.Expression before, Ir.Expression after) {
      budget.Step();
      if (before.Type != after.Type || before.Type is not ("bool" or "int" or "real")) { return false; }
      switch (before, after) {
        case (Ir.BooleanLiteral a, Ir.BooleanLiteral b): return a.Value == b.Value;
        case (Ir.IntegerLiteral a, Ir.IntegerLiteral b): return a.Value == b.Value;
        case (Ir.RationalLiteral a, Ir.RationalLiteral b): return a.Numerator == b.Numerator && a.Denominator == b.Denominator;
        case (Ir.Variable a, Ir.Variable b): return a.Type is "int" or "real" && a.Name == b.Name && a.ResultType == b.ResultType;
        case (Ir.Label a, Ir.Label b): return a.Name == b.Name && SameNumeric(a.Body, b.Body);
        case (Ir.Operation a, Ir.Operation b):
          if (a.Operator != b.Operator || a.ResultType != b.ResultType || a.Arguments.Count != b.Arguments.Count) { return false; }
          for (var i = 0; i < a.Arguments.Count; i++) {
            if (!SameNumeric(a.Arguments[i], b.Arguments[i])) { return false; }
          }
          return true;
        default: return false;
      }
    }
  }
}
