// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
#nullable enable
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace Microsoft.Dafny;

internal sealed record B3RealPreparationLimits(int MaximumRows = 1024,
  int MaximumFreeMemberships = 100000, int MaximumWork = 800000,
  int MaximumOutputNodes = Ir.Protocol.MaximumNodes, int MaximumDepth = Ir.Protocol.MaximumDepth);

internal sealed record B3RealPreparationEvidence(string ProducerVersion, string MaskId,
  string OriginalProgramHash, string FinalProgramHash, bool Applied, string? DeclineReason);
internal sealed record B3RealPreparedRequests(IReadOnlyList<Ir.Request> Requests,
  IReadOnlyList<B3RealPreparationEvidence> Evidence,
  ImmutableArray<ImmutableArray<B3DefinitionOrigin>> Definitions);

internal sealed class B3RealPreparationRejection : Exception {
  internal IToken Token { get; }
  internal B3RealPreparationRejection(string message, IToken token) : base(message) { Token = token; }
}
internal sealed class B3RealOptionalLimit : Exception {
  internal B3RealOptionalLimit(string message) : base(message) { }
}

/// <summary>Prepares submissions only after the caller validates the original G3/unsigned replay.</summary>
internal static class B3RealContextPreparation {
  internal const string ProducerVersion = "typed-floor-embedding-1";
  internal const int MaximumCaptureSlots = 4 * Ir.Protocol.MaximumNodes;
  internal const int MaximumAggregateCaptureSlots = 4 * B3DefinitionContexts.MaximumAggregateNodes;
  private sealed record SourceWitness(B3VerificationContext Context, Ir.Request Request,
    string MaskId, ImmutableArray<B3DefinitionOrigin> Definitions);

  internal static B3RealPreparedRequests Prepare(IReadOnlyList<B3VerificationContext> contexts,
    IReadOnlyList<Ir.Request> requests, IToken token, B3RealPreparationLimits? limits = null) {
    limits ??= new B3RealPreparationLimits();
    try {
      Require(contexts != null && requests != null, "Missing original Real context/request inventory", token);
      var count = requests.Count; var contextCount = contexts.Count;
      Require(count is > 0 and <= B3DefinitionContexts.MaximumContexts && contextCount == count,
        "Original Real preparation context inventory differs", token);
      Require(limits.MaximumRows is > 0 and <= 1024 && limits.MaximumFreeMemberships is > 0 and <= 100000 &&
        limits.MaximumWork is > 0 and <= 800000 && limits.MaximumOutputNodes is > 0 and <= Ir.Protocol.MaximumNodes &&
        limits.MaximumDepth is > 0 and <= Ir.Protocol.MaximumDepth, "Invalid optional Real preparation limits", token);
      var originals = new Ir.Request[count];
      var witnesses = new SourceWitness[count];
      var originalEscapedTextBytes = new long[count];
      var maskIds = new HashSet<string>(StringComparer.Ordinal);
      long aggregateNodes = 0, aggregateBytes = 0, aggregateSlots = 0;
      for (var i = 0; i < originals.Length; i++) {
        var context = contexts[i]; var request = requests[i];
        Require(context != null && request != null && context.Definitions != null,
          "Missing original Real context/request", token);
        Require(context.MaskId is { Length: > 0 and <= 4096 } && context.Definitions.Count <= 64 && maskIds.Add(context.MaskId),
          "Original Real mask evidence exceeds its bound", token);
        Require(ReferenceEquals(context.Program, request.Program) && ReferenceEquals(context.Obligations, request.Obligations),
          "Original Real request is detached from its validated context", token);
        var snapshot = Capture(request, token, out var nodes, out var slots, out var escapedTextBytes,
          (int)Math.Min(Ir.Protocol.MaximumNodes, B3DefinitionContexts.MaximumAggregateNodes - aggregateNodes),
          (int)Math.Min(MaximumCaptureSlots, MaximumAggregateCaptureSlots - aggregateSlots));
        witnesses[i] = new(context, request, context.MaskId, CaptureDefinitions(context.Definitions, token));
        originalEscapedTextBytes[i] = checked(escapedTextBytes + (snapshot.ProgramHash.Length == 0 ? 384 : 0));
        Require(originalEscapedTextBytes[i] <= Ir.Protocol.MaximumMessageBytes,
          "Original Real canonical hash exceeds encoded text budget", token);
        var byteAllowance = (int)Math.Min(Ir.Protocol.MaximumMessageBytes,
          B3DefinitionContexts.MaximumAggregateBytes - aggregateBytes);
        _ = JsonSize(snapshot, byteAllowance - 64); // Reserve the canonical hash in an empty-hash header.
        var hash = Hash(snapshot.Program);
        Require(snapshot.ProgramHash.Length == 0 || snapshot.ProgramHash == hash,
          "Original Real program hash changed during capture", token);
        originals[i] = snapshot with { ProgramHash = hash };
        var bytes = JsonSize(originals[i], byteAllowance);
        Ir.ProtocolValidation.ValidateRequest(originals[i]);
        aggregateNodes += nodes; aggregateBytes += bytes; aggregateSlots += slots;
        Require(aggregateNodes <= B3DefinitionContexts.MaximumAggregateNodes &&
          aggregateBytes <= B3DefinitionContexts.MaximumAggregateBytes && aggregateSlots <= MaximumAggregateCaptureSlots,
          "Original Real snapshots exceed aggregate bounds", token);
      }
      // Every fallback below is this owned, completely typed/hash-bound snapshot.
      // Re-capture live source only for association checks; it never supplies fallback data.
      AssertSourceUnchanged(contexts, requests, originals, witnesses, token);
      var work = new B3RealRoundTrip.Work(limits);
      var plans = new B3RealRoundTrip.StatementPlan[originals.Length];
      long validationAllowance = 0;
      string? decline = null;
      try {
        long finalNodes = 0, finalBytes = 0;
        for (var i = 0; i < originals.Length; i++) {
          plans[i] = B3RealRoundTrip.Plan(originals[i].Program.Unit.Body, work);
          var fixedNodes = originalFixedNodes(originals[i]);
          var nodes = checked(fixedNodes + plans[i].Nodes);
          // A whole original request is a safe reserve for unchanged headers/declarations.
          var bytes = checked(Math.Max(JsonSize(originals[i]), originalEscapedTextBytes[i]) + plans[i].Bytes);
          if (nodes > limits.MaximumOutputNodes || bytes > Ir.Protocol.MaximumMessageBytes ||
              plans[i].Height > limits.MaximumDepth) { throw new B3RealOptionalLimit("Real output growth exceeds its optional bound"); }
          finalNodes += nodes; finalBytes += bytes;
          if (finalNodes > B3DefinitionContexts.MaximumAggregateNodes || finalBytes > B3DefinitionContexts.MaximumAggregateBytes) {
            throw new B3RealOptionalLimit("Real output growth exceeds its aggregate bound");
          }
        }
        // The independent checker needs at most twice recipe work plus four
        // visits per expanded target occurrence (including exact barrier comparison).
        validationAllowance = checked(2 * work.Count + 4 * plans.Sum(plan => plan.Nodes));
        work.Step(validationAllowance);
      } catch (B3RealOptionalLimit failure) { decline = failure.Message; }
      var submitted = new Ir.Request[originals.Length];
      var evidence = new B3RealPreparationEvidence[originals.Length];
      if (decline != null) {
        for (var i = 0; i < originals.Length; i++) {
          submitted[i] = originals[i];
          evidence[i] = new(ProducerVersion, witnesses[i].MaskId, originals[i].ProgramHash, originals[i].ProgramHash, false, decline);
        }
      } else {
        var relationBudget = new B3RealRoundTripRelation.Budget(validationAllowance);
        // All-mask growth/byte/work admission precedes any target materialization.
        for (var i = 0; i < originals.Length; i++) {
          var program = originals[i].Program with { Unit = originals[i].Program.Unit with { Body = plans[i].Materialize() } };
          submitted[i] = originals[i] with { Program = program, ProgramHash = Hash(program) };
          Ir.ProtocolValidation.ValidateRequest(submitted[i]);
          evidence[i] = new(ProducerVersion, witnesses[i].MaskId, originals[i].ProgramHash, submitted[i].ProgramHash,
            originals[i].ProgramHash != submitted[i].ProgramHash, null);
          B3RealRoundTripRelation.Validate(originals[i], submitted[i], evidence[i], token, limits, relationBudget);
        }
      }
      AssertSourceUnchanged(contexts, requests, originals, witnesses, token);
      return new(submitted.ToImmutableArray(), evidence.ToImmutableArray(),
        witnesses.Select(witness => witness.Definitions).ToImmutableArray());
    } catch (B3RealPreparationRejection) { throw; }
      catch (Exception error) when (error is InvalidDataException or JsonException or OverflowException or ArgumentException or
          IndexOutOfRangeException or NullReferenceException) {
        throw new B3RealPreparationRejection("Real preparation rejected: " + error.Message, token);
      }
  }

  private static long originalFixedNodes(Ir.Request request) => request.Program.Types.Count + request.Program.Functions.Count +
    request.Program.Functions.Sum(function => (long)function.Parameters.Count) + request.Program.Axioms.Count +
    request.Program.Unit.Variables.Count + request.Obligations.Count +
    request.Program.Axioms.Sum(axiom => B3RealRoundTrip.ExpressionSize(axiom.Condition).Nodes);

  private static void AssertSourceUnchanged(IReadOnlyList<B3VerificationContext> contexts,
    IReadOnlyList<Ir.Request> live, IReadOnlyList<Ir.Request> owned, IReadOnlyList<SourceWitness> witnesses, IToken token) {
    Require(contexts.Count == owned.Count && live.Count == owned.Count, "Real source context inventory changed", token);
    for (var i = 0; i < owned.Count; i++) {
      Require(ReferenceEquals(contexts[i], witnesses[i].Context) && ReferenceEquals(live[i], witnesses[i].Request) &&
        contexts[i].MaskId == witnesses[i].MaskId && CaptureDefinitions(contexts[i].Definitions, token).SequenceEqual(witnesses[i].Definitions) &&
        ReferenceEquals(contexts[i].Program, live[i].Program) && ReferenceEquals(contexts[i].Obligations, live[i].Obligations),
        "Real source context association changed", token);
      var current = Capture(live[i], token, out _, out _, out _);
      Require(Hash(current.Program) == owned[i].ProgramHash && (current.ProgramHash.Length == 0 || current.ProgramHash == owned[i].ProgramHash) &&
        current.Obligations.SequenceEqual(owned[i].Obligations) &&
        JsonEqual(current.Configuration, owned[i].Configuration) && current.RequestId == owned[i].RequestId &&
        current.UnitId == owned[i].UnitId && current.B3Commit == owned[i].B3Commit && current.Version == owned[i].Version &&
        current.NormalizerVersion == owned[i].NormalizerVersion && current.WorkerFingerprint == owned[i].WorkerFingerprint,
        "Real source request changed after original validation", token);
    }
  }

  private static ImmutableArray<B3DefinitionOrigin> CaptureDefinitions(IReadOnlyList<B3DefinitionOrigin> definitions, IToken token) {
    Require(definitions != null && definitions.Count <= 64, "Real definition-origin inventory exceeds its bound", token);
    var state = new CaptureState(token, 64, 64);
    return state.List(definitions, origin => {
      Require(origin != null, "Missing Real definition origin", token);
      state.Text(origin.Id); state.Text(origin.Owner); state.Text(origin.FormulaHash); state.Text(origin.Instance);
      return origin;
    });
  }

  // Independent relation entry admission; this does not certify source availability.
  internal static Ir.Request CaptureForRelation(Ir.Request request, IToken token) {
    var snapshot = Capture(request, token, out _, out _, out _);
    _ = JsonSize(snapshot);
    Require(snapshot.ProgramHash == Hash(snapshot.Program), "Real relation input hash changed during capture", token);
    return snapshot;
  }

  internal static string Hash(Ir.Program program) {
    var buffer = Json(program);
    return Convert.ToHexString(SHA256.HashData(buffer.WrittenSpan)).ToLowerInvariant();
  }
  internal static string ConfigurationHash(Ir.Configuration configuration) {
    var buffer = Json(configuration);
    return Convert.ToHexString(SHA256.HashData(buffer.WrittenSpan)).ToLowerInvariant();
  }
  internal static int JsonSize<T>(T value, int limit = Ir.Protocol.MaximumMessageBytes) => Json(value, limit).Count;
  internal static bool JsonEqual<T>(T left, T right) => Json(left).WrittenSpan.SequenceEqual(Json(right).WrittenSpan);
  private static BoundedBuffer Json<T>(T value, int limit = Ir.Protocol.MaximumMessageBytes) {
    var buffer = new BoundedBuffer(limit);
    using var writer = new Utf8JsonWriter(buffer);
    JsonSerializer.Serialize(writer, value, Ir.Protocol.JsonOptions); writer.Flush();
    return buffer;
  }
  private sealed class BoundedBuffer : IBufferWriter<byte> {
    private byte[] bytes;
    private readonly int limit;
    internal int Count { get; private set; }
    internal ReadOnlySpan<byte> WrittenSpan => bytes.AsSpan(0, Count);
    internal BoundedBuffer(int limit) {
      if (limit < 1) { throw new InvalidDataException("Real JSON aggregate allocation allowance exhausted"); }
      this.limit = limit; bytes = new byte[Math.Min(256, limit)];
    }
    public void Advance(int count) {
      if (count < 0 || count > bytes.Length - Count) { throw new InvalidDataException("Invalid bounded JSON advancement"); }
      Count += count;
    }
    public Memory<byte> GetMemory(int sizeHint = 0) { Ensure(sizeHint); return bytes.AsMemory(Count); }
    public Span<byte> GetSpan(int sizeHint = 0) { Ensure(sizeHint); return bytes.AsSpan(Count); }
    private void Ensure(int sizeHint) {
      sizeHint = Math.Max(1, sizeHint);
      if (sizeHint > limit - Count) { throw new InvalidDataException("Real request exceeds bounded JSON allocation"); }
      var needed = Count + sizeHint;
      if (needed > bytes.Length) { Array.Resize(ref bytes, Math.Max(needed, Math.Min(limit, bytes.Length * 2))); }
    }
  }

  private static Ir.Request Capture(Ir.Request request, IToken token, out long nodes, out long slots, out long escapedTextBytes,
    int maximumNodes = Ir.Protocol.MaximumNodes, int maximumSlots = MaximumCaptureSlots) {
    Require(request != null && request.Program != null && request.Program.Unit != null && request.Configuration != null,
      "Missing original Real request header", token);
    var capture = new CaptureState(token, maximumNodes, maximumSlots);
    var program = request.Program;
    var result = request with {
      Program = new Ir.Program(capture.List(program.Types, type => { capture.Visit(0); return capture.Text(type); }),
        capture.List(program.Functions, function => {
          capture.Visit(0); return new Ir.Function(capture.Text(function.Name), capture.List(function.Parameters,
            binding => capture.Binding(binding, 0)), capture.Text(function.ResultType));
        }), capture.List(program.Axioms, axiom => {
          capture.Visit(0); return new Ir.Axiom(capture.List(axiom.Explains, capture.Text), capture.Expression(axiom.Condition, 0));
        }), new Ir.Unit(capture.Text(program.Unit.Name), capture.List(program.Unit.Variables, binding => capture.Binding(binding, 0)),
          capture.Statement(program.Unit.Body, 0))),
      Obligations = capture.List(request.Obligations, identity => {
        capture.Visit(0); capture.Text(identity.Id); capture.Text(identity.Uri); capture.Text(identity.Description);
        return identity; // SourceIdentity is sealed, flat immutable data; preserve the exact original record.
      }),
      Configuration = request.Configuration with { SolverArguments = capture.List(request.Configuration.SolverArguments, capture.Text) }
    };
    capture.Text(request.RequestId); capture.Text(request.ProgramHash); capture.Text(request.UnitId);
    capture.Text(request.B3Commit); capture.Text(request.WorkerFingerprint); capture.Text(request.NormalizerVersion);
    capture.Text(request.Configuration.SolverExecutable); capture.Text(request.Configuration.SolverVersion); capture.Text(request.Configuration.SolverSha256);
    nodes = capture.Nodes; slots = capture.Slots; escapedTextBytes = capture.EscapedTextBytes; return result;
  }
  private sealed class CaptureState {
    private readonly IToken token;
    private readonly int maximumNodes;
    private readonly int maximumSlots;
    internal long Nodes { get; private set; }
    internal long Slots { get; private set; }
    private long textCharacters;
    internal long EscapedTextBytes { get; private set; }
    internal CaptureState(IToken token, int maximumNodes, int maximumSlots) {
      this.token = token; this.maximumNodes = maximumNodes; this.maximumSlots = maximumSlots;
    }
    internal void Visit(int depth) {
      Require(depth <= Ir.Protocol.MaximumDepth && ++Nodes <= maximumNodes,
        "Original Real capture exceeds node/depth bounds", token);
    }
    internal string Text(string value) {
      if (value == null || value.Length > Ir.Protocol.MaximumMessageBytes) {
        throw new B3RealPreparationRejection("Original Real capture has invalid/oversized text", token);
      }
      textCharacters = checked(textCharacters + value.Length);
      Require(textCharacters <= Ir.Protocol.MaximumMessageBytes, "Original Real capture text budget exceeded", token);
      // A JSON string uses at most \uXXXX per UTF-16 code unit, plus quotes.
      // Charge before any encoder can allocate its complete escaped token.
      EscapedTextBytes = checked(EscapedTextBytes + 6L * value.Length + 2);
      Require(EscapedTextBytes <= Ir.Protocol.MaximumMessageBytes, "Original Real capture encoded text budget exceeded", token);
      return value;
    }
    internal ImmutableArray<TOut> List<TIn, TOut>(IReadOnlyList<TIn> values, Func<TIn, TOut> copy) {
      if (values == null) {
        throw new B3RealPreparationRejection("Missing original Real capture list", token);
      }
      var count = values.Count;
      if (count < 0 || count > maximumSlots - Slots) {
        throw new B3RealPreparationRejection("Original Real capture list exceeds its allocation bound", token);
      }
      Slots += count; // Charge the complete array before allocating any of its slots.
      var result = ImmutableArray.CreateBuilder<TOut>(count);
      for (var i = 0; i < count; i++) { result.Add(copy(values[i])); }
      Require(values.Count == count, "Original Real capture list changed", token);
      return result.MoveToImmutable();
    }
    internal Ir.Binding Binding(Ir.Binding binding, int depth) {
      Visit(depth); return new Ir.Binding(Text(binding.Name), Text(binding.Type));
    }
    internal Ir.Expression Expression(Ir.Expression expression, int depth) {
      Visit(depth);
      Text(expression.Type); // The base Type is serialized independently of derived result fields.
      Require(expression switch {
        Ir.BooleanLiteral => expression.Type == "bool",
        Ir.IntegerLiteral => expression.Type == "int",
        Ir.RationalLiteral => expression.Type == "real",
        Ir.BitvectorLiteral word => expression.Type == Ir.Protocol.BitvectorTypeName(word.Width),
        Ir.Variable variable => variable.Type == variable.ResultType,
        Ir.Application application => application.Type == application.ResultType,
        Ir.Operation operation => operation.Type == operation.ResultType,
        Ir.BitvectorOperation word => word.Type == word.ResultType,
        Ir.Let let => let.Body != null && let.Type == let.Body.Type,
        Ir.Label label => label.Body != null && label.Type == label.Body.Type,
        Ir.Quantifier => expression.Type == "bool",
        _ => false
      }, "Original Real expression has inconsistent type aliases", token);
      return expression switch {
        Ir.BooleanLiteral boolean => boolean,
        Ir.IntegerLiteral integer => new Ir.IntegerLiteral(Text(integer.Value)),
        Ir.RationalLiteral rational => new Ir.RationalLiteral(Text(rational.Numerator), Text(rational.Denominator)),
        Ir.BitvectorLiteral word => new Ir.BitvectorLiteral(Text(word.Value), word.Width),
        Ir.Variable variable => new Ir.Variable(Text(variable.Name), Text(variable.ResultType)),
        Ir.Application application => new Ir.Application(Text(application.Name), Text(application.ResultType),
          List(application.Arguments, child => Expression(child, depth + 1))),
        Ir.Operation operation => new Ir.Operation(operation.Operator, Text(operation.ResultType),
          List(operation.Arguments, child => Expression(child, depth + 1))),
        Ir.BitvectorOperation word => new Ir.BitvectorOperation(word.Operator, word.Width, word.Start, word.End,
          Text(word.ResultType), List(word.Arguments, child => Expression(child, depth + 1))),
        Ir.Quantifier quantifier => new Ir.Quantifier(quantifier.Universal,
          List(quantifier.Bindings, binding => Binding(binding, depth + 1)),
          List(quantifier.Patterns, pattern => (IReadOnlyList<Ir.Expression>)List(pattern, child => Expression(child, depth + 1))),
          Expression(quantifier.Body, depth + 1)),
        Ir.Let let => new Ir.Let(Binding(let.Binding, depth + 1), Expression(let.Value, depth + 1), Expression(let.Body, depth + 1)),
        Ir.Label label => new Ir.Label(Text(label.Name), Expression(label.Body, depth + 1)),
        _ => throw new B3RealPreparationRejection("Unknown original Real expression", token)
      };
    }
    internal Ir.Statement Statement(Ir.Statement statement, int depth) {
      Visit(depth);
      return statement switch {
        Ir.Block block => new Ir.Block(List(block.Statements, child => Statement(child, depth + 1))),
        Ir.Assign assign => new Ir.Assign(Text(assign.Variable), Expression(assign.Value, depth + 1)),
        Ir.Havoc havoc => new Ir.Havoc(List(havoc.Variables, Text)),
        Ir.Check check => new Ir.Check(Text(check.ObligationId), Expression(check.Condition, depth + 1), check.Learn),
        Ir.Assume assume => new Ir.Assume(Expression(assume.Condition, depth + 1)),
        Ir.Choice choice => new Ir.Choice(List(choice.Branches, child => Statement(child, depth + 1))),
        Ir.Conditional conditional => new Ir.Conditional(Expression(conditional.Condition, depth + 1),
          Statement(conditional.Then, depth + 1), Statement(conditional.Else, depth + 1)),
        Ir.Loop loop => new Ir.Loop(List(loop.Invariants, expression => Expression(expression, depth + 1)), Statement(loop.Body, depth + 1)),
        Ir.Labeled labeled => new Ir.Labeled(Text(labeled.Name), Statement(labeled.Body, depth + 1)),
        Ir.Exit exit => new Ir.Exit(Text(exit.Label)),
        Ir.Return => new Ir.Return(),
        _ => throw new B3RealPreparationRejection("Unknown original Real statement", token)
      };
    }
  }
  internal static void Require([DoesNotReturnIf(false)] bool condition, string message, IToken token) {
    if (!condition) { throw new B3RealPreparationRejection(message, token); }
  }
}
