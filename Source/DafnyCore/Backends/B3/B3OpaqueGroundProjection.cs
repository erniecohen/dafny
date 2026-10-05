// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace Microsoft.Dafny;

internal sealed record B3OpaqueGroundLimits(int MaximumWitnessSlots = 200000, int MaximumWork = 800000);
internal sealed record B3OpaqueGroundEvidence(string ProducerVersion, string MaskId,
  string InputProgramHash, string FinalProgramHash, bool Applied, string? DeclineReason);
internal sealed record B3OpaqueGroundPreparedRequests(ImmutableArray<Ir.Request> Requests,
  ImmutableArray<B3OpaqueGroundEvidence> Evidence, long WitnessSlots);

/// <summary>Optional projection of an independently checked, closed ground fragment.</summary>
internal static class B3OpaqueGroundProjection {
  internal const string ProducerVersion = "opaque-ground-singletons-1";
  private sealed class Ineligible : Exception {
    internal Ineligible(string message) : base(message) { }
  }
  internal sealed class Work {
    private readonly B3OpaqueGroundLimits limits;
    internal long Count { get; private set; }
    internal long Slots { get; private set; }
    internal Work(B3OpaqueGroundLimits limits) { this.limits = limits; }
    internal void Step(long count = 1) {
      Count = checked(Count + count);
      if (Count > limits.MaximumWork) { throw new B3RealOptionalLimit("Opaque projection work allowance exhausted"); }
    }
    internal void Slot(long count = 1) {
      Step(count); Slots = checked(Slots + count);
      if (Slots > limits.MaximumWitnessSlots) { throw new B3RealOptionalLimit("Opaque projection witness allowance exhausted"); }
    }
  }

  internal static B3OpaqueGroundPreparedRequests Prepare(B3RealPreparedRequests source, IToken token,
    B3OpaqueGroundLimits? limits = null) {
    limits ??= new();
    try {
      Require(limits.MaximumWitnessSlots is > 0 and <= 200000 && limits.MaximumWork is > 0 and <= 800000,
        "Invalid opaque projection optional limits", token);
      Require(source != null && source.Requests is ImmutableArray<Ir.Request> &&
        source.Evidence is ImmutableArray<B3RealPreparationEvidence> && !source.Definitions.IsDefault,
        "Opaque projection requires the owned Phase A inventory", token);
      var count = source.Requests.Count;
      Require(count is > 0 and <= B3DefinitionContexts.MaximumContexts && source.Evidence.Count == count &&
        source.Definitions.Length == count, "Opaque projection inventories disagree", token);
      var masks = new HashSet<string>(StringComparer.Ordinal);
      long nodes = 0, slots = 0, bytes = 0;
      var outputReserves = new long[count];
      // No recipe, target or hash is allocated before the complete owned input audit.
      for (var i = 0; i < count; i++) {
        var evidence = source.Evidence[i];
        Require(evidence != null && evidence.ProducerVersion == B3RealContextPreparation.ProducerVersion &&
          evidence.MaskId is { Length: > 0 and <= 4096 } && masks.Add(evidence.MaskId) &&
          evidence.OriginalProgramHash is { Length: 64 } && evidence.FinalProgramHash is { Length: 64 } &&
          (evidence.DeclineReason == null || evidence.DeclineReason.Length <= 4096) &&
          !source.Definitions[i].IsDefault && source.Definitions[i].Length <= 64,
          "Opaque projection source/mask association differs", token);
        var audit = AuditOwned(source.Requests[i], token);
        audit.Text(evidence.ProducerVersion); audit.Text(evidence.MaskId);
        audit.Text(evidence.OriginalProgramHash); audit.Text(evidence.FinalProgramHash);
        if (evidence.DeclineReason != null) { audit.Text(evidence.DeclineReason); }
        foreach (var origin in source.Definitions[i]) {
          Require(origin != null && origin.Id != null && origin.Owner != null && origin.FormulaHash != null && origin.Instance != null &&
            origin.Id.Length <= 4096 && origin.Owner.Length <= Ir.Protocol.MaximumMessageBytes &&
            origin.FormulaHash.Length <= 4096 && origin.Instance.Length <= 4096, "Invalid owned definition origin", token);
          audit.Text(origin.Id); audit.Text(origin.Owner); audit.Text(origin.FormulaHash); audit.Text(origin.Instance);
        }
        nodes = checked(nodes + audit.Nodes); slots = checked(slots + audit.Slots);
        bytes = checked(bytes + audit.EscapedTextBytes); outputReserves[i] = audit.UpperBytes;
        Require(nodes <= B3DefinitionContexts.MaximumAggregateNodes && slots <= B3RealContextPreparation.MaximumAggregateCaptureSlots &&
          bytes <= B3DefinitionContexts.MaximumAggregateBytes, "Opaque projection owned inputs exceed aggregate bounds", token);
      }
      long actualBytes = 0;
      for (var i = 0; i < count; i++) {
        var request = source.Requests[i];
        actualBytes = checked(actualBytes + B3RealContextPreparation.JsonSize(request));
        Require(actualBytes <= B3DefinitionContexts.MaximumAggregateBytes, "Opaque owned serialized aggregate exceeds its bound", token);
        Require(source.Evidence[i].FinalProgramHash == request.ProgramHash &&
          B3RealContextPreparation.Hash(request.Program) == request.ProgramHash,
          "Opaque projection input is detached from Phase A output", token);
        Ir.ProtocolValidation.ValidateRequest(request);
      }
      var work = new Work(limits);
      var plans = new Plan?[count]; var reasons = new string?[count];
      string? aggregateDecline = null;
      long relationAllowance = 0;
      try {
        // Recipes preserve original slots, so their conservative output count/bytes
        // never exceed the complete input audit (including copied occurrences).
        long outputBytes = 0;
        for (var i = 0; i < count; i++) {
          outputBytes = checked(outputBytes + outputReserves[i]);
          if (outputReserves[i] > Ir.Protocol.MaximumMessageBytes || outputBytes > B3DefinitionContexts.MaximumAggregateBytes) {
            throw new B3RealOptionalLimit("Opaque output growth exceeds its conservative byte allowance");
          }
          try {
            if (source.Definitions[i].Length != 0) { throw new Ineligible("Owned definition mask is nonempty"); }
            plans[i] = new Producer(source.Requests[i].Program, work).Plan();
          } catch (Ineligible failure) { reasons[i] = failure.Message; }
        }
        // Reserve independently recomputed classification and exact comparisons
        // before ANY target arrays. The checker has its own decrementing budget.
        relationAllowance = checked(12 * nodes + 4 * slots + count * 128L);
        work.Step(relationAllowance);
      } catch (B3RealOptionalLimit failure) { aggregateDecline = failure.Message; }
      var requests = ImmutableArray.CreateBuilder<Ir.Request>(count);
      var evidenceRows = ImmutableArray.CreateBuilder<B3OpaqueGroundEvidence>(count);
      for (var i = 0; i < count; i++) {
        var original = source.Requests[i];
        var decline = aggregateDecline ?? reasons[i];
        var target = original;
        if (decline == null) {
          var program = original.Program with {
            Types = ImmutableArray<string>.Empty, Functions = ImmutableArray<Ir.Function>.Empty,
            Unit = original.Program.Unit with {
              Variables = RetainedBindings(original.Program.Unit.Variables),
              Body = plans[i]!.Materialize()
            }
          };
          var hash = B3RealContextPreparation.Hash(program);
          if (hash != original.ProgramHash) { target = original with { Program = program, ProgramHash = hash }; }
        }
        requests.Add(target);
        evidenceRows.Add(new(ProducerVersion, source.Evidence[i].MaskId, original.ProgramHash, target.ProgramHash,
          original.ProgramHash != target.ProgramHash, decline));
      }
      var submitted = requests.MoveToImmutable(); var evidence = evidenceRows.MoveToImmutable();
      // At most one bounded original/target pair is captured by each relation call.
      // Optional fallback uses the completely typed owned inputs, never live source.
      var budget = new B3OpaqueGroundProjectionRelation.Budget(aggregateDecline == null ? relationAllowance : 800000);
      for (var i = 0; i < count; i++) {
        B3OpaqueGroundProjectionRelation.Validate(source.Requests[i], submitted[i], evidence[i], source.Evidence[i].MaskId,
          source.Definitions[i], token, budget);
      }
      return new(submitted, evidence, work.Slots);
    } catch (B3RealPreparationRejection) { throw; }
      catch (Exception error) when (error is InvalidDataException or JsonException or OverflowException or ArgumentException or
          IndexOutOfRangeException or NullReferenceException) {
        throw new B3RealPreparationRejection("Opaque projection rejected: " + error.Message, token);
      }
  }

  private static ImmutableArray<Ir.Binding> RetainedBindings(IReadOnlyList<Ir.Binding> bindings) {
    var count = 0;
    foreach (var binding in bindings) { if (binding.Type is "int" or "real") { count++; } }
    var retained = ImmutableArray.CreateBuilder<Ir.Binding>(count);
    foreach (var binding in bindings) { if (binding.Type is "int" or "real") { retained.Add(binding); } }
    return retained.MoveToImmutable();
  }

  private sealed record ExpressionPlan(Ir.Expression Source, ExpressionPlan? Left = null, ExpressionPlan? Right = null, bool Erase = false) {
    internal Ir.Expression Materialize() {
      if (Erase) { return new Ir.BooleanLiteral(true); }
      if (Left == null) { return Source; }
      var operation = (Ir.Operation)Source; var left = Left.Materialize(); var right = Right!.Materialize();
      return ReferenceEquals(left, operation.Arguments[0]) && ReferenceEquals(right, operation.Arguments[1]) ? Source :
        operation with { Arguments = ImmutableArray.Create(left, right) };
    }
  }
  private sealed record Plan(Ir.Statement Source, ImmutableArray<Plan> Children = default,
    ExpressionPlan? Assumption = null, bool Erase = false) {
    internal Ir.Statement Materialize() => Erase ? new Ir.Block(ImmutableArray<Ir.Statement>.Empty) : Source switch {
      Ir.Block => MaterializeBlock(),
      Ir.Assume assume => MaterializeAssumption(assume),
      _ => Source
    };
    private Ir.Statement MaterializeBlock() {
      var children = ImmutableArray.CreateBuilder<Ir.Statement>(Children.Length);
      foreach (var child in Children) { children.Add(child.Materialize()); }
      return new Ir.Block(children.MoveToImmutable());
    }
    private Ir.Statement MaterializeAssumption(Ir.Assume assume) {
      var value = Assumption!.Materialize();
      return ReferenceEquals(value, assume.Condition) ? assume : new Ir.Assume(value);
    }
  }
  private sealed class Producer {
    private readonly Ir.Program program;
    private readonly Work work;
    private readonly HashSet<string> sorts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Ir.Function> functions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> variables = new(StringComparer.Ordinal);
    private readonly HashSet<string> assignedBooleans = new(StringComparer.Ordinal);
    private bool returned;
    internal Producer(Ir.Program program, Work work) { this.program = program; this.work = work; }
    private void Eligible([DoesNotReturnIf(false)] bool condition, string reason) { work.Step(); if (!condition) { throw new Ineligible(reason); } }
    internal Plan Plan() {
      Eligible(program.Axioms.Count == 0, "Source axioms are nonempty");
      foreach (var sort in program.Types) { work.Slot(); sorts.Add(sort); }
      foreach (var function in program.Functions) {
        work.Slot();
        Eligible(function.ResultType == "bool" || sorts.Contains(function.ResultType), "Function result has numeric or word dependencies");
        foreach (var parameter in function.Parameters) { work.Step(); Eligible(sorts.Contains(parameter.Type), "Function parameter is not opaque"); }
        functions.Add(function.Name, function);
      }
      foreach (var binding in program.Unit.Variables) {
        work.Slot(); Eligible(binding.Type is "bool" or "int" or "real" || sorts.Contains(binding.Type), "Binding has native word dependencies");
        variables.Add(binding.Name, binding.Type);
        if (binding.Type is "int" or "real") { work.Slot(); } // Future target binding-array occurrence.
      }
      var result = Statement(program.Unit.Body);
      Eligible(returned, "Ground projection needs one terminal Return"); return result;
    }
    private Plan Statement(Ir.Statement statement) {
      work.Slot();
      if (statement is Ir.Block block) {
        work.Slot(block.Statements.Count); // Charge the full recipe array before its builder.
        var children = ImmutableArray.CreateBuilder<Plan>(block.Statements.Count);
        foreach (var child in block.Statements) { children.Add(Statement(child)); }
        return new(statement, children.MoveToImmutable());
      }
      Eligible(!returned, "Nonempty statement follows terminal Return");
      switch (statement) {
        case Ir.Return: returned = true; return new(statement);
        case Ir.Assign assign:
          if (variables[assign.Variable] is "int" or "real") { Numeric(assign.Value); return new(statement); }
          if (variables[assign.Variable] == "bool") {
            Eligible(assign.Value is Ir.BooleanLiteral || assign.Value is Ir.Variable copy && assignedBooleans.Contains(copy.Name),
              "Removed Boolean value is not literal or definitely assigned copy");
            work.Slot(); assignedBooleans.Add(assign.Variable);
          } else { Ground(assign.Value); }
          return new(statement, Erase: true);
        case Ir.Check check: Numeric(check.Condition); return new(statement);
        case Ir.Assume assume: return new(statement, Assumption: Assumption(assume.Condition));
        default: throw new Ineligible("Control is outside the straight-line ground grammar");
      }
    }
    private ExpressionPlan Assumption(Ir.Expression expression) {
      work.Slot();
      if (expression is Ir.Operation { Operator: Ir.Operator.And, Arguments.Count: 2 } conjunction) {
        return new(expression, Assumption(conjunction.Arguments[0]), Assumption(conjunction.Arguments[1]));
      }
      if (expression is Ir.BooleanLiteral { Value: true }) { return new(expression); }
      if (expression is Ir.Operation { Operator: Ir.Operator.Equal, Arguments.Count: 2 } equality &&
          sorts.Contains(equality.Arguments[0].Type) && equality.Arguments[0].Type == equality.Arguments[1].Type) {
        Ground(equality.Arguments[0]); Ground(equality.Arguments[1]); return new(expression, Erase: true);
      }
      if (expression is Ir.Application application && application.Type == "bool") {
        Predicate(application); return new(expression, Erase: true);
      }
      Numeric(expression); return new(expression);
    }
    private void Predicate(Ir.Application application) {
      work.Step(); Eligible(functions.TryGetValue(application.Name, out var function) && function.ResultType == "bool",
        "Unknown opaque predicate");
      foreach (var argument in application.Arguments) { Ground(argument); }
    }
    private void Ground(Ir.Expression expression) {
      work.Step(); Eligible(sorts.Contains(expression.Type), "Ground term is not opaque");
      switch (expression) {
        case Ir.Variable variable: Eligible(variables[variable.Name] == variable.Type, "Wrong ground variable"); break;
        case Ir.Application application:
          Eligible(functions.TryGetValue(application.Name, out var function) && function.ResultType == application.Type,
            "Unknown ground function");
          foreach (var argument in application.Arguments) { Ground(argument); } break;
        default: throw new Ineligible("Opaque expression is not a ground variable/application");
      }
    }
    private void Numeric(Ir.Expression expression) {
      work.Step(); Eligible(expression.Type is "bool" or "int" or "real", "Retained expression has opaque or word type");
      switch (expression) {
        case Ir.BooleanLiteral or Ir.IntegerLiteral or Ir.RationalLiteral: break;
        case Ir.Variable variable: Eligible(variable.Type is "int" or "real", "Retained Boolean variable is removed"); break;
        case Ir.Operation operation: foreach (var argument in operation.Arguments) { Numeric(argument); } break;
        case Ir.Label label: Numeric(label.Body); break;
        default: throw new Ineligible("Retained expression has a binder, word or function application");
      }
    }
  }

  // This is allocation admission only. It knows no singleton/projection rules.
  // All nested collections must be the immutable ones produced by Phase A.
  internal sealed class OwnedAudit {
    private readonly IToken token;
    internal long Nodes { get; private set; }
    internal long Slots { get; private set; }
    private long text;
    internal long EscapedTextBytes => text;
    internal long UpperBytes => checked(text + 512 * Nodes + 64 * Slots + 4096);
    internal OwnedAudit(IToken token) { this.token = token; }
    internal void Visit(int depth = 0) {
      Require(depth <= Ir.Protocol.MaximumDepth && ++Nodes <= Ir.Protocol.MaximumNodes, "Opaque owned input exceeds node/depth bounds", token);
    }
    internal void Text(string value) {
      Require(value != null && value.Length <= Ir.Protocol.MaximumMessageBytes, "Opaque owned input has invalid text", token);
      text = checked(text + 6L * value.Length + 2);
      Require(text <= Ir.Protocol.MaximumMessageBytes, "Opaque owned input encoded text exceeds its bound", token);
    }
    internal void List<T>(IReadOnlyList<T> list, Action<T> visit) {
      Require(list is ImmutableArray<T> immutable && !immutable.IsDefault, "Opaque projection input collection is not owned/immutable", token);
      Slots = checked(Slots + list.Count);
      Require(Slots <= B3RealContextPreparation.MaximumCaptureSlots, "Opaque owned input slot bound exceeded", token);
      foreach (var item in list) { visit(item); }
    }
    internal void Binding(Ir.Binding binding, int depth) { Visit(depth); Text(binding.Name); Text(binding.Type); }
    internal void Expression(Ir.Expression expression, int depth) {
      Visit(depth); Text(expression.Type);
      Require(expression switch {
        Ir.BooleanLiteral => expression.Type == "bool", Ir.IntegerLiteral => expression.Type == "int",
        Ir.RationalLiteral => expression.Type == "real", Ir.BitvectorLiteral word => expression.Type == Ir.Protocol.BitvectorTypeName(word.Width),
        Ir.Variable variable => variable.Type == variable.ResultType, Ir.Application application => application.Type == application.ResultType,
        Ir.Operation operation => operation.Type == operation.ResultType, Ir.BitvectorOperation word => word.Type == word.ResultType,
        Ir.Quantifier => expression.Type == "bool", Ir.Let let => let.Type == let.Body.Type,
        Ir.Label label => label.Type == label.Body.Type, _ => false
      }, "Opaque owned expression has inconsistent type aliases", token);
      switch (expression) {
        case Ir.BooleanLiteral: break;
        case Ir.IntegerLiteral integer: Text(integer.Value); break;
        case Ir.RationalLiteral rational: Text(rational.Numerator); Text(rational.Denominator); break;
        case Ir.BitvectorLiteral word: Text(word.Value); break;
        case Ir.Variable variable: Text(variable.Name); Text(variable.ResultType); break;
        case Ir.Application application: Text(application.Name); Text(application.ResultType); List(application.Arguments, child => Expression(child, depth + 1)); break;
        case Ir.Operation operation: Text(operation.ResultType); List(operation.Arguments, child => Expression(child, depth + 1)); break;
        case Ir.BitvectorOperation word: Text(word.ResultType); List(word.Arguments, child => Expression(child, depth + 1)); break;
        case Ir.Quantifier quantifier:
          List(quantifier.Bindings, binding => Binding(binding, depth + 1));
          List(quantifier.Patterns, pattern => List(pattern, child => Expression(child, depth + 1))); Expression(quantifier.Body, depth + 1); break;
        case Ir.Let let: Binding(let.Binding, depth + 1); Expression(let.Value, depth + 1); Expression(let.Body, depth + 1); break;
        case Ir.Label label: Text(label.Name); Expression(label.Body, depth + 1); break;
        default: Require(false, "Unknown owned expression", token); break;
      }
    }
    internal void Statement(Ir.Statement statement, int depth) {
      Visit(depth);
      switch (statement) {
        case Ir.Block block: List(block.Statements, child => Statement(child, depth + 1)); break;
        case Ir.Assign assign: Text(assign.Variable); Expression(assign.Value, depth + 1); break;
        case Ir.Havoc havoc: List(havoc.Variables, Text); break;
        case Ir.Check check: Text(check.ObligationId); Expression(check.Condition, depth + 1); break;
        case Ir.Assume assume: Expression(assume.Condition, depth + 1); break;
        case Ir.Choice choice: List(choice.Branches, child => Statement(child, depth + 1)); break;
        case Ir.Conditional conditional: Expression(conditional.Condition, depth + 1); Statement(conditional.Then, depth + 1); Statement(conditional.Else, depth + 1); break;
        case Ir.Loop loop: List(loop.Invariants, expression => Expression(expression, depth + 1)); Statement(loop.Body, depth + 1); break;
        case Ir.Labeled labeled: Text(labeled.Name); Statement(labeled.Body, depth + 1); break;
        case Ir.Exit exit: Text(exit.Label); break;
        case Ir.Return: break;
        default: Require(false, "Unknown owned statement", token); break;
      }
    }
  }
  internal static OwnedAudit AuditOwned(Ir.Request request, IToken token) {
    var audit = new OwnedAudit(token); var program = request.Program;
    audit.List(program.Types, type => { audit.Visit(); audit.Text(type); });
    audit.List(program.Functions, function => {
      audit.Visit(); audit.Text(function.Name); audit.Text(function.ResultType);
      audit.List(function.Parameters, binding => audit.Binding(binding, 0));
    });
    audit.List(program.Axioms, axiom => {
      audit.Visit(); audit.List(axiom.Explains, audit.Text); audit.Expression(axiom.Condition, 0);
    });
    audit.Text(program.Unit.Name); audit.List(program.Unit.Variables, binding => audit.Binding(binding, 0));
    audit.Statement(program.Unit.Body, 0);
    audit.List(request.Obligations, origin => { audit.Visit(); audit.Text(origin.Id); audit.Text(origin.Uri); audit.Text(origin.Description); });
    audit.List(request.Configuration.SolverArguments, audit.Text);
    audit.Text(request.Configuration.SolverExecutable); audit.Text(request.Configuration.SolverVersion); audit.Text(request.Configuration.SolverSha256);
    audit.Text(request.RequestId); audit.Text(request.ProgramHash); audit.Text(request.UnitId); audit.Text(request.NormalizerVersion);
    audit.Text(request.B3Commit); audit.Text(request.WorkerFingerprint);
    return audit;
  }
  private static void Require([DoesNotReturnIf(false)] bool condition, string message, IToken token) => B3RealContextPreparation.Require(condition, message, token);
}
