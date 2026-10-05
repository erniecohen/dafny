// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Microsoft.BaseTypes;
using Bpl = Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace Microsoft.Dafny;

#nullable disable

/// <summary>Exact source shape is separate from a direct retained-root certificate.</summary>
internal sealed class B3UnsignedWrappers {
  internal sealed class Rejection(string message, Bpl.IToken token) : Exception(message) {
    internal Bpl.IToken Token { get; } = token;
  }
  private sealed record Shape(Bpl.Function Wrapper, Bpl.Function Native, Bpl.Axiom Axiom,
    Bpl.Variable Binder, int Width, Bpl.Variable WrapperInput, Bpl.Variable WrapperOutput,
    Bpl.Variable NativeInput, Bpl.Variable NativeOutput);
  private sealed record RawRoot(Bpl.Block Block, IReadOnlyList<Bpl.StateCmd> Containers, Bpl.Cmd Command);
  private sealed class ConditionalAnchor(Ir.Conditional statement) {
    internal Ir.Conditional Statement { get; } = statement;
    internal string StatementPath;
  }
  private sealed class Scope(RawRoot root, B3StructuredCfgCorrespondence.IfGuardCertificate guard = null) {
    internal B3StructuredCfgCorrespondence.IfGuardCertificate Guard { get; } = guard;
    internal B3IfGuardSnapshot Snapshot;
    internal ConditionalAnchor Conditional;

    internal RawRoot Root { get; } = root;
    internal List<Witness> Calls { get; } = new();
    internal Dictionary<Bpl.NAryExpr, string> SourceCalls { get; } = new(ReferenceEqualityComparer.Instance);
  }
  private sealed record Witness(Shape Shape, Scope Scope, Bpl.NAryExpr SourceCall, string SourcePath,
    Ir.Application Application, Bpl.Expr Argument, Bpl.Variable ArgumentDeclaration) {
    internal HashSet<Ir.Statement> Anchors { get; } = new(ReferenceEqualityComparer.Instance);
    internal HashSet<string> GuardPaths { get; } = new(StringComparer.Ordinal);
  }
  private readonly Bpl.Program source;
  private readonly Bpl.Implementation unit;
  private readonly Dictionary<Bpl.Function, Shape> shapes = new(ReferenceEqualityComparer.Instance);
  private readonly Dictionary<Bpl.Cmd, RawRoot> roots = new(ReferenceEqualityComparer.Instance);
  private readonly Dictionary<Ir.Application, Witness> witnesses = new(ReferenceEqualityComparer.Instance);
  private readonly Dictionary<string, Shape> symbols = new(StringComparer.Ordinal);
  private Scope active;
  private readonly B3StructuredCfgCorrespondence.IfGuardCatalogue guardCatalogue;
  private Ir.Statement boundOriginalBody;
  private HashSet<Ir.Application> certifiedApplications = new(ReferenceEqualityComparer.Instance);
  private int sourceNodes;
  private long pathCharacters;
  private static readonly FieldInfo ProxyTarget = typeof(Bpl.TypeProxy).GetField("proxyFor",
    BindingFlags.Instance | BindingFlags.NonPublic);

  internal B3UnsignedWrappers(Bpl.Program source, Bpl.Implementation unit,
    B3StructuredCfgCorrespondence.IfGuardCatalogue guardCatalogue = null) {
    this.guardCatalogue = guardCatalogue;
    this.source = source; this.unit = unit;
    var functions = new HashSet<Bpl.Function>(ReferenceEqualityComparer.Instance);
    var axioms = new List<Bpl.Axiom>();
    var declarations = 0;
    foreach (var declaration in source.TopLevelDeclarations) {
      Require(++declarations <= Ir.Protocol.MaximumNodes, "Wrapper declaration index exceeds its bound", unit.tok);
      if (declaration is Bpl.Function function) { functions.Add(function); }
      if (declaration is Bpl.Axiom axiom) { axioms.Add(axiom); }
    }
    foreach (var axiom in axioms) {
      if (!TryShape(axiom, functions, out var shape)) { continue; }
      Require(shapes.Count < 64 && !shapes.ContainsKey(shape.Wrapper),
        "Wrapper shape index is oversized or has an ambiguous source equality", axiom.tok);
      shapes.Add(shape.Wrapper, shape);
    }
    if (shapes.Count == 0) { return; }
    foreach (var block in unit.Blocks) {
      foreach (var command in block.Cmds) { IndexRoot(block, command, Array.Empty<Bpl.StateCmd>(), 0); }
    }
  }

  private void IndexRoot(Bpl.Block block, Bpl.Cmd command, IReadOnlyList<Bpl.StateCmd> containers, int depth) {
    Require(depth < Ir.Protocol.MaximumDepth && ++sourceNodes <= Ir.Protocol.MaximumNodes && !roots.ContainsKey(command),
      "Wrapper raw command index has ambiguous identities or exceeds its bound", command.tok);
    roots.Add(command, new RawRoot(block, containers, command));
    if (command.GetType() == typeof(Bpl.StateCmd)) {
      var state = (Bpl.StateCmd)command;
      foreach (var child in state.Cmds) { IndexRoot(block, child, containers.Append(state).ToArray(), depth + 1); }
    }
  }

  internal Ir.Statement InCommand(Bpl.Cmd command, Func<Ir.Statement> normalize) {
    var previous = active;
    var eligible = command.GetType() == typeof(Bpl.AssertCmd) || command.GetType() == typeof(Bpl.AssumeCmd) ||
      command.GetType() == typeof(Bpl.AssignCmd) && ((Bpl.AssignCmd)command).Lhss.All(lhs => lhs is Bpl.SimpleAssignLhs);
    active = eligible && roots.TryGetValue(command, out var root) ? new Scope(root) : null;
    try {
      if (active != null) { IndexSourceCalls(active); }
      var result = normalize();
      if (active != null && active.Calls.Count != 0) {
        Walk(result, (statement, expression, path) => {
          if (expression is Ir.Application application && witnesses.TryGetValue(application, out var witness) &&
              ReferenceEquals(witness.Scope, active)) { witness.Anchors.Add(statement); }
        });
      }
      return result;
    } finally { active = previous; }
  }

  internal Ir.Conditional InIfGuard(Bpl.IfCmd owner, Func<Ir.Expression> normalizeGuard,
    Ir.Statement thenStatement, Ir.Statement elseStatement) {
    var previous = active;
    active = guardCatalogue != null && guardCatalogue.TryGet(unit, owner, out var certificate) &&
      roots.TryGetValue(certificate.Positive.Command, out var root) && ReferenceEquals(root.Block, certificate.Positive.Block) ?
      new Scope(root, certificate) : null;
    try {
      if (active != null) {
        IndexSourceCalls(active);
        if (active.SourceCalls.Count != 0) {
          active.Snapshot = B3IfGuardSnapshot.Capture(active.Guard.Guard, ref sourceNodes, ref pathCharacters);
        }
      }
      var result = new Ir.Conditional(normalizeGuard(), thenStatement, elseStatement);
      if (active != null && active.Calls.Count != 0) { active.Conditional = new ConditionalAnchor(result); }
      return result;
    } finally { active = previous; }
  }

  // Bind only after the full original tree is assembled. Replay clones Conditional
  // records but preserves its Condition and ordered statement/expression paths.
  internal void BindOriginalGuardOccurrences(Ir.Program original) {
    var guarded = witnesses.Values.Where(witness => witness.Scope.Guard != null).ToArray();
    if (guarded.Length == 0) { return; }
    Require(boundOriginalBody == null, "Original guard occurrences were already bound", unit.tok);
    boundOriginalBody = original.Unit.Body;
    Walk(boundOriginalBody, (statement, expression, path) => {
      if (expression is not Ir.Application application || !witnesses.TryGetValue(application, out var witness) ||
          witness.Scope.Guard == null) { return; }
      var anchor = witness.Scope.Conditional;
      var statementPath = StatementPath(path);
      Require(anchor != null && ReferenceEquals(statement, anchor.Statement) &&
        (anchor.StatementPath == null || anchor.StatementPath == statementPath),
        "Guard application has an ambiguous or foreign original Conditional occurrence", unit.tok);
      anchor.StatementPath = statementPath;
      Require(witness.GuardPaths.Add(path), "Guard original occurrence path is duplicate", unit.tok);
      pathCharacters += path.Length;
      Require(pathCharacters <= Ir.Protocol.MaximumMessageBytes, "Guard original occurrence paths exceed their bound", unit.tok);
    });
    Require(guarded.All(witness => witness.GuardPaths.Count != 0 && witness.Scope.Conditional.StatementPath != null),
      "Captured guard application is absent from its original Conditional", unit.tok);
  }
  private static string StatementPath(string expressionPath) {
    var separator = expressionPath.IndexOf("/expr", StringComparison.Ordinal);
    Require(separator >= 0, "Guard occurrence has no expression slot path", Bpl.Token.NoToken);
    return expressionPath[..separator];
  }

  internal void Capture(Bpl.NAryExpr call, Ir.Application application) {
    if (call.Fun is not Bpl.FunctionCall function || !shapes.TryGetValue(function.Func, out var shape)) { return; }
    Require(call.Type?.IsInt == true && Monomorphic(call) && call.Args.Count == 1 &&
      Width(call.Args[0].Type) == shape.Width && application.Type == "int" && application.Arguments.Count == 1 &&
      application.ResultType == "int" && application.Arguments[0].Type == Ir.Protocol.BitvectorTypeName(shape.Width),
      "Wrapper call differs from its exact resolved source signature", call.tok);
    Require(active != null, "Unsigned wrapper call has no captured direct raw-command root", call.tok);
    Require(active.SourceCalls.TryGetValue(call, out var sourcePath), "Unsigned wrapper call is not inside its active raw expression root", call.tok);
    Require(witnesses.Count < Ir.Protocol.MaximumNodes && !witnesses.ContainsKey(application),
      "Wrapper call witness inventory exceeds its bound or reuses a target identity", call.tok);
    pathCharacters += sourcePath.Length;
    Require(pathCharacters <= Ir.Protocol.MaximumMessageBytes, "Wrapper witness paths exceed their bound", call.tok);
    if (symbols.TryGetValue(application.Name, out var other)) {
      Require(ReferenceEquals(other, shape), "Wrapper target symbol has conflicting source owners", call.tok);
    } else { symbols.Add(application.Name, shape); }
    var witness = new Witness(shape, active, call, sourcePath, application, call.Args[0],
      (call.Args[0] as Bpl.IdentifierExpr)?.Decl);
    witnesses.Add(application, witness); active.Calls.Add(witness);
  }

  private void IndexSourceCalls(Scope scope) {
    var command = scope.Root.Command;
    var expressions = CommandExpressions(command);
    var pending = new Stack<(Bpl.Expr Expression, string Path, int Depth)>();
    var slot = 0;
    foreach (var expression in expressions) { pending.Push((expression, "root/" + slot++, 0)); }
    while (pending.Count != 0) {
      var (expression, path, depth) = pending.Pop();
      Require(depth < Ir.Protocol.MaximumDepth && ++sourceNodes <= Ir.Protocol.MaximumNodes,
        "Wrapper source expression traversal exceeds its bound", command.tok);
      if (expression is Bpl.NAryExpr { Fun: Bpl.FunctionCall call } application && shapes.ContainsKey(call.Func)) {
        scope.SourceCalls.TryAdd(application, path);
      }
      var index = 0;
      foreach (var child in SourceChildren(expression)) { pending.Push((child, path + "/" + index++, depth + 1)); }
    }
  }
  private static IEnumerable<Bpl.Expr> CommandExpressions(Bpl.Cmd command) => command switch {
    Bpl.AssertCmd assertion => new[] { assertion.Expr }, Bpl.AssumeCmd assumption => new[] { assumption.Expr },
    Bpl.AssignCmd assignment => assignment.Rhss, _ => Array.Empty<Bpl.Expr>()
  };

  private void RecheckSourceCalls(Scope scope, ref int count) {
    var expected = scope.Calls.GroupBy(witness => witness.SourcePath, StringComparer.Ordinal)
      .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
    var pending = new Stack<(Bpl.Expr Expression, string Path, int Depth)>();
    var slot = 0;
    foreach (var expression in CommandExpressions(scope.Root.Command)) { pending.Push((expression, "root/" + slot++, 0)); }
    while (pending.Count != 0) {
      var (expression, path, depth) = pending.Pop();
      Require(depth < Ir.Protocol.MaximumDepth && ++count <= Ir.Protocol.MaximumNodes,
        "Wrapper source certificate recheck exceeds its bound", scope.Root.Command.tok);
      if (expected.Remove(path, out var calls)) {
        foreach (var witness in calls) {
          var call = witness.SourceCall;
          Require(ReferenceEquals(expression, call) && call.Fun is Bpl.FunctionCall function &&
            ReferenceEquals(function.Func, witness.Shape.Wrapper) && call.Type?.IsInt == true && Monomorphic(call) &&
            call.Args.Count == 1 && ReferenceEquals(call.Args[0], witness.Argument) &&
            Width(call.Args[0].Type) == witness.Shape.Width &&
            (witness.ArgumentDeclaration == null || call.Args[0] is Bpl.IdentifierExpr identifier &&
              ReferenceEquals(identifier.Decl, witness.ArgumentDeclaration)),
            "Captured unsigned call, argument or signature changed at its exact source path", call.tok);
        }
      }
      var index = 0;
      foreach (var child in SourceChildren(expression)) { pending.Push((child, path + "/" + index++, depth + 1)); }
    }
    Require(expected.Count == 0, "Captured unsigned call is missing from its exact source path", scope.Root.Command.tok);
  }

  private static IEnumerable<Bpl.Expr> SourceChildren(Bpl.Expr expression) {
    switch (expression) {
      case Bpl.NAryExpr application: return application.Args;
      case Bpl.OldExpr old: return new[] { old.Expr };
      case Bpl.BvExtractExpr extract: return new[] { extract.Bitvector };
      case Bpl.BvConcatExpr concat: return new[] { concat.E0, concat.E1 };
      case Bpl.QuantifierExpr quantifier: {
        var children = new List<Bpl.Expr> { quantifier.Body }; var count = 0;
        for (var trigger = quantifier.Triggers; trigger != null; trigger = trigger.Next) {
          Require(++count <= Ir.Protocol.MaximumNodes && children.Count + trigger.Tr.Count() <= Ir.Protocol.MaximumNodes,
            "Wrapper trigger traversal exceeds its bound", expression.tok);
          children.AddRange(trigger.Tr);
        }
        return children;
      }
      case Bpl.LetExpr let: return let.Rhss.Append(let.Body);
      // Actual Body expansion and lambda bodies do not supply direct source roots.
      default: return Array.Empty<Bpl.Expr>();
    }
  }

  internal Ir.Program Lower(Ir.Program original, IReadOnlyList<Ir.SourceIdentity> obligations,
    IReadOnlyDictionary<string, IReadOnlyList<B3DefinitionContexts.Formula>> definitions,
    IReadOnlyList<B3VerificationContext> preliminary) {
    if (symbols.Count == 0) { return original; }
    var owned = new HashSet<Bpl.Function>(source.TopLevelDeclarations.OfType<Bpl.Function>(), ReferenceEqualityComparer.Instance);
    foreach (var shape in shapes.Values) {
      Require(source.TopLevelDeclarations.Any(declaration => ReferenceEquals(declaration, shape.Axiom)) && TryShape(shape.Axiom, owned, out var current) &&
        ReferenceEquals(current.Wrapper, shape.Wrapper) && ReferenceEquals(current.Native, shape.Native) &&
        ReferenceEquals(current.Binder, shape.Binder) && current.Width == shape.Width &&
        ReferenceEquals(current.WrapperInput, shape.WrapperInput) && ReferenceEquals(current.WrapperOutput, shape.WrapperOutput) &&
        ReferenceEquals(current.NativeInput, shape.NativeInput) && ReferenceEquals(current.NativeOutput, shape.NativeOutput),
        "Unsigned wrapper source shape changed after capture", unit.tok);
    }
    var guardScopes = witnesses.Values.Select(witness => witness.Scope).Distinct()
      .Where(scope => scope.Guard != null).ToArray();
    if (guardScopes.Length != 0) {
      Require(guardCatalogue != null && ReferenceEquals(boundOriginalBody, original.Unit.Body),
        "Guard occurrences have no bound complete original tree", unit.tok);
      guardCatalogue.Recheck();
    }
    var recheckNodes = 0; long recheckBytes = 0;
    foreach (var scope in witnesses.Values.Select(witness => witness.Scope).Distinct()) {
      IReadOnlyList<Bpl.Cmd> commands = scope.Root.Block.Cmds;
      Require(unit.Blocks.Any(block => ReferenceEquals(block, scope.Root.Block)),
        "Captured wrapper block is no longer owned by this unit", unit.tok);
      foreach (var container in scope.Root.Containers) {
        Require(commands.Any(command => ReferenceEquals(command, container)),
          "Captured wrapper StateCmd chain is no longer owned by its raw root", unit.tok);
        commands = container.Cmds;
      }
      Require(commands.Any(command => ReferenceEquals(command, scope.Root.Command)),
        "Captured wrapper command is no longer owned by its raw root", unit.tok);
      if (scope.Guard != null) {
        Require(scope.Snapshot != null && ReferenceEquals(scope.Root.Command, scope.Guard.Positive.Command) &&
          ReferenceEquals(scope.Root.Block, scope.Guard.Positive.Block) &&
          ReferenceEquals(((Bpl.AssumeCmd)scope.Root.Command).Expr, scope.Guard.Guard),
          "Guard witness has lost its own positive producer root", unit.tok);
        scope.Snapshot.Recheck(scope.Guard.Guard, ref recheckNodes, ref recheckBytes);
      }
      RecheckSourceCalls(scope, ref recheckNodes);
    }
    B3DefinitionContexts.ValidatePartition(original, obligations, preliminary, unit.tok);
    foreach (var formulas in definitions.Values) {
      foreach (var formula in formulas) {
        WalkExpression(formula.Condition, (expression, _) => Require(
          expression is not Ir.Application application || !symbols.ContainsKey(application.Name),
          "Unsigned wrapper occurrence in a selected definition has no direct source certificate", unit.tok));
      }
    }
    var certified = new HashSet<Ir.Application>(ReferenceEqualityComparer.Instance);
    var receipts = new HashSet<(string Mask, string Path)>();
    long receiptCharacters = 0;
    foreach (var context in preliminary) {
      var selected = context.Obligations.Select(identity => identity.Id).ToHashSet(StringComparer.Ordinal);
      Walk(context.Program.Unit.Body, (statement, expression, path) => {
        if (expression is not Ir.Application application || !symbols.ContainsKey(application.Name)) { return; }
        Require(witnesses.TryGetValue(application, out var witness),
          "Retained unsigned wrapper occurrence has no source-call identity witness", unit.tok);
        Require(Available(witness, statement, selected, path),
          "Retained unsigned wrapper occurrence lacks its own source root in this context", unit.tok);
        Require(receipts.Count < B3DefinitionContexts.MaximumAggregateNodes && receipts.Add((context.MaskId, path)),
          "Wrapper context occurrence receipts are duplicate or exceed their bound", unit.tok);
        receiptCharacters += context.MaskId.Length + path.Length;
        Require(receiptCharacters <= B3DefinitionContexts.MaximumAggregateBytes,
          "Wrapper context receipt paths exceed their byte bound", unit.tok);
        certified.Add(application);
      });
    }
    certifiedApplications = certified;
    var lowered = original with { Unit = original.Unit with { Body = Rewrite(original.Unit.Body, certified) } };
    ValidateSubstitution(original, lowered, certified);
    return lowered;
  }

  internal void ValidateContexts(IReadOnlyList<B3VerificationContext> preliminary, IReadOnlyList<B3VerificationContext> final) {
    Require(preliminary.Count == final.Count, "Wrapper substitution changed context inventory", unit.tok);
    for (var i = 0; i < preliminary.Count; i++) {
      var before = preliminary[i]; var after = final[i];
      Require(before.MaskId == after.MaskId && before.Obligations.SequenceEqual(after.Obligations) &&
        before.Definitions.SequenceEqual(after.Definitions), "Wrapper substitution changed context identities or definitions", unit.tok);
      ValidateSubstitution(before.Program, after.Program, certifiedApplications);
    }
  }

  private bool Available(Witness witness, Ir.Statement statement, HashSet<string> selected, string path) {
    // Capturing a shape or source expression elsewhere cannot supply this invocation's active root.
    if (!unit.Blocks.Any(block => ReferenceEquals(block, witness.Scope.Root.Block)) ||
        !roots.TryGetValue(witness.Scope.Root.Command, out var root) || !ReferenceEquals(root, witness.Scope.Root)) { return false; }
    if (witness.Scope.Guard != null) {
      var anchor = witness.Scope.Conditional;
      return anchor != null && statement is Ir.Conditional conditional &&
        ReferenceEquals(conditional.Condition, anchor.Statement.Condition) && conditional.Condition.Type == "bool" &&
        anchor.StatementPath == StatementPath(path) && witness.GuardPaths.Contains(path) &&
        ReferenceEquals(witness.Scope.Root.Command, witness.Scope.Guard.Positive.Command) &&
        ReferenceEquals(witness.Scope.Root.Block, witness.Scope.Guard.Positive.Block);
    }
    foreach (var anchor in witness.Anchors) {
      if (ReferenceEquals(anchor, statement)) {
        return anchor switch {
          Ir.Check check => witness.Scope.Root.Command is Bpl.AssertCmd && selected.Contains(check.ObligationId),
          Ir.Assume => witness.Scope.Root.Command is Bpl.AssumeCmd,
          Ir.Assign => witness.Scope.Root.Command is Bpl.AssignCmd,
          _ => false
        };
      }
      if (anchor is Ir.Check { Learn: true } learned && !selected.Contains(learned.ObligationId) &&
          statement is Ir.Assume assumed && ReferenceEquals(learned.Condition, assumed.Condition) &&
          witness.Scope.Root.Command is Bpl.AssertCmd) { return true; }
    }
    return false;
  }

  private Ir.Statement Rewrite(Ir.Statement statement, HashSet<Ir.Application> certified) => statement switch {
    Ir.Block block => block with { Statements = block.Statements.Select(child => Rewrite(child, certified)).ToArray() },
    Ir.Assign assign => assign with { Value = Rewrite(assign.Value, certified) },
    Ir.Check check => check with { Condition = Rewrite(check.Condition, certified) },
    Ir.Assume assume => assume with { Condition = Rewrite(assume.Condition, certified) },
    Ir.Choice choice => choice with { Branches = choice.Branches.Select(child => Rewrite(child, certified)).ToArray() },
    Ir.Conditional conditional => conditional with { Condition = Rewrite(conditional.Condition, certified),
      Then = Rewrite(conditional.Then, certified), Else = Rewrite(conditional.Else, certified) },
    Ir.Loop loop => loop with { Invariants = loop.Invariants.Select(invariant => Rewrite(invariant, certified)).ToArray(), Body = Rewrite(loop.Body, certified) },
    Ir.Labeled labeled => labeled with { Body = Rewrite(labeled.Body, certified) },
    Ir.Havoc or Ir.Exit or Ir.Return => statement,
    _ => throw new Rejection("Unknown statement in wrapper substitution", unit.tok)
  };
  private Ir.Expression Rewrite(Ir.Expression expression, HashSet<Ir.Application> certified) {
    switch (expression) {
      case Ir.Application application: {
        var arguments = application.Arguments.Select(argument => Rewrite(argument, certified)).ToArray();
        if (symbols.ContainsKey(application.Name)) {
          Require(certified.Contains(application) && witnesses.TryGetValue(application, out _),
            "Original wrapper occurrence was not certified in its retained context", unit.tok);
          var witness = witnesses[application];
          return new Ir.BitvectorOperation(Ir.BitvectorOperator.BitvectorToUnsignedInt, witness.Shape.Width, 0, 0, "int", arguments);
        }
        return application with { Arguments = arguments };
      }
      case Ir.Operation operation: return operation with { Arguments = operation.Arguments.Select(argument => Rewrite(argument, certified)).ToArray() };
      case Ir.BitvectorOperation word: return word with { Arguments = word.Arguments.Select(argument => Rewrite(argument, certified)).ToArray() };
      case Ir.Quantifier quantifier: return quantifier with { Body = Rewrite(quantifier.Body, certified),
        Patterns = quantifier.Patterns.Select(pattern => (IReadOnlyList<Ir.Expression>)pattern.Select(term => Rewrite(term, certified)).ToArray()).ToArray() };
      case Ir.Let let: return let with { Value = Rewrite(let.Value, certified), Body = Rewrite(let.Body, certified) };
      case Ir.Label label: return label with { Body = Rewrite(label.Body, certified) };
      case Ir.BooleanLiteral or Ir.IntegerLiteral or Ir.RationalLiteral or Ir.BitvectorLiteral or Ir.Variable: return expression;
      default: throw new Rejection("Unknown expression in wrapper substitution", unit.tok);
    }
  }

  private void ValidateSubstitution(Ir.Program before, Ir.Program after, HashSet<Ir.Application> certified) {
    Require(ReferenceEquals(before.Types, after.Types) && ReferenceEquals(before.Functions, after.Functions) &&
      before.Axioms.Count == after.Axioms.Count && before.Axioms.Select((axiom, index) =>
        axiom.Explains.SequenceEqual(after.Axioms[index].Explains) && ReferenceEquals(axiom.Condition, after.Axioms[index].Condition)).All(same => same) &&
      before.Unit.Name == after.Unit.Name &&
      ReferenceEquals(before.Unit.Variables, after.Unit.Variables), "Wrapper substitution changed original declarations", unit.tok);
    var pending = new Stack<(object Before, object After, int Depth)>(); pending.Push((before.Unit.Body, after.Unit.Body, 0));
    var count = 0;
    while (pending.Count != 0) {
      var (left, right, depth) = pending.Pop();
      Require(depth <= Ir.Protocol.MaximumDepth && ++count <= Ir.Protocol.MaximumNodes,
        "Wrapper substitution validation exceeds its bound", unit.tok);
      Require(left != null && right != null, "Wrapper substitution introduced a null node", unit.tok);
      void Pair(object x, object y) => pending.Push((x, y, depth + 1));
      void List<T>(IReadOnlyList<T> x, IReadOnlyList<T> y) {
        Require(x != null && y != null && x.Count == y.Count, "Wrapper substitution changed child inventory", unit.tok);
        for (var i = 0; i < x.Count; i++) { Pair(x[i], y[i]); }
      }
      if (left is Ir.Application wrapper && symbols.TryGetValue(wrapper.Name, out var shape)) {
        Require(certified.Contains(wrapper) && witnesses.ContainsKey(wrapper) && right is Ir.BitvectorOperation {
          Operator: Ir.BitvectorOperator.BitvectorToUnsignedInt, Start: 0, End: 0 } replacement &&
          replacement.Width == shape.Width && replacement.Type == "int" && replacement.ResultType == "int" &&
          wrapper.Type == "int" && wrapper.ResultType == "int" &&
          replacement.Arguments.Count == 1 && wrapper.Arguments.Count == 1,
          "Unsigned wrapper replacement differs from its certified operation", unit.tok);
        List(wrapper.Arguments, ((Ir.BitvectorOperation)right).Arguments); continue;
      }
      Require(left.GetType() == right.GetType(), "Wrapper substitution changed an unrelated constructor", unit.tok);
      switch (left) {
        case Ir.Block block: List(block.Statements, ((Ir.Block)right).Statements); break;
        case Ir.Assign assign: Require(assign.Variable == ((Ir.Assign)right).Variable, "Assignment target changed", unit.tok); Pair(assign.Value, ((Ir.Assign)right).Value); break;
        case Ir.Check check:
          var checkedAfter = (Ir.Check)right;
          Require(check.ObligationId == checkedAfter.ObligationId && check.Learn == checkedAfter.Learn, "Check identity or learning changed", unit.tok);
          Pair(check.Condition, checkedAfter.Condition); break;
        case Ir.Assume assume: Pair(assume.Condition, ((Ir.Assume)right).Condition); break;
        case Ir.Choice choice: List(choice.Branches, ((Ir.Choice)right).Branches); break;
        case Ir.Conditional conditional:
          var branch = (Ir.Conditional)right; Pair(conditional.Condition, branch.Condition); Pair(conditional.Then, branch.Then); Pair(conditional.Else, branch.Else); break;
        case Ir.Loop loop: List(loop.Invariants, ((Ir.Loop)right).Invariants); Pair(loop.Body, ((Ir.Loop)right).Body); break;
        case Ir.Labeled labeled: Require(labeled.Name == ((Ir.Labeled)right).Name, "Control label changed", unit.tok); Pair(labeled.Body, ((Ir.Labeled)right).Body); break;
        case Ir.Havoc havoc: Require(havoc.Variables.SequenceEqual(((Ir.Havoc)right).Variables), "Havoc target inventory changed", unit.tok); break;
        case Ir.Exit exit: Require(exit.Label == ((Ir.Exit)right).Label, "Exit changed", unit.tok); break;
        case Ir.Return: break;
        case Ir.Application application:
          var app = (Ir.Application)right; Require(application.Name == app.Name && application.Type == app.Type && application.ResultType == app.ResultType, "Unrelated function application changed", unit.tok); List(application.Arguments, app.Arguments); break;
        case Ir.Operation operation:
          var op = (Ir.Operation)right; Require(operation.Operator == op.Operator && operation.Type == op.Type && operation.ResultType == op.ResultType, "Unrelated operation changed", unit.tok); List(operation.Arguments, op.Arguments); break;
        case Ir.BitvectorOperation wordBefore:
          var word = (Ir.BitvectorOperation)right;
          Require(wordBefore.Operator == word.Operator && wordBefore.Width == word.Width && wordBefore.Start == word.Start &&
            wordBefore.End == word.End && wordBefore.Type == word.Type && wordBefore.ResultType == word.ResultType, "Word operation metadata changed", unit.tok); List(wordBefore.Arguments, word.Arguments); break;
        case Ir.Quantifier quantifier:
          var quant = (Ir.Quantifier)right;
          Require(quantifier.Universal == quant.Universal && quantifier.Type == quant.Type && quantifier.Bindings.SequenceEqual(quant.Bindings) &&
            quantifier.Patterns.Count == quant.Patterns.Count, "Quantifier metadata changed", unit.tok);
          Pair(quantifier.Body, quant.Body); for (var i = 0; i < quantifier.Patterns.Count; i++) { List(quantifier.Patterns[i], quant.Patterns[i]); } break;
        case Ir.Let let:
          var binding = (Ir.Let)right; Require(let.Binding == binding.Binding && let.Type == binding.Type, "Let binding changed", unit.tok);
          Pair(let.Value, binding.Value); Pair(let.Body, binding.Body); break;
        case Ir.Label label:
          var tagged = (Ir.Label)right; Require(label.Name == tagged.Name && label.Type == tagged.Type, "Expression label changed", unit.tok); Pair(label.Body, tagged.Body); break;
        case Ir.BooleanLiteral or Ir.IntegerLiteral or Ir.RationalLiteral or Ir.BitvectorLiteral or Ir.Variable:
          Require(left.Equals(right), "Literal or variable changed", unit.tok); break;
        default: throw new Rejection("Unknown constructor in narrow wrapper validator", unit.tok);
      }
    }
  }

  private void Walk(Ir.Statement root, Action<Ir.Statement, Ir.Expression, string> visit) {
    var pending = new Stack<(Ir.Statement Statement, string Path, int Depth)>(); pending.Push((root, "body", 0));
    var count = 0;
    while (pending.Count != 0) {
      var (statement, path, depth) = pending.Pop();
      Require(depth <= Ir.Protocol.MaximumDepth && ++count <= Ir.Protocol.MaximumNodes, "Wrapper statement traversal exceeds its bound", unit.tok);
      var index = 0;
      foreach (var expression in StatementExpressions(statement)) {
        WalkExpression(expression, (child, suffix) => visit(statement, child, path + "/expr" + index + suffix), depth + 1); index++;
      }
      index = 0;
      foreach (var child in StatementChildren(statement)) { pending.Push((child, path + "/stmt" + index++, depth + 1)); }
    }
  }
  private void WalkExpression(Ir.Expression root, Action<Ir.Expression, string> visit, int initialDepth = 0) {
    var pending = new Stack<(Ir.Expression Expression, string Path, int Depth)>(); pending.Push((root, "", initialDepth));
    var count = 0;
    while (pending.Count != 0) {
      var (expression, path, depth) = pending.Pop();
      Require(depth <= Ir.Protocol.MaximumDepth && ++count <= Ir.Protocol.MaximumNodes, "Wrapper expression traversal exceeds its bound", unit.tok);
      visit(expression, path); var index = 0;
      foreach (var child in ExpressionChildren(expression)) { pending.Push((child, path + "/" + index++, depth + 1)); }
    }
  }
  private static IEnumerable<Ir.Expression> StatementExpressions(Ir.Statement statement) => statement switch {
    Ir.Assign assign => new[] { assign.Value }, Ir.Check check => new[] { check.Condition }, Ir.Assume assume => new[] { assume.Condition },
    Ir.Conditional conditional => new[] { conditional.Condition }, Ir.Loop loop => loop.Invariants,
    Ir.Block or Ir.Choice or Ir.Labeled or Ir.Havoc or Ir.Exit or Ir.Return => Array.Empty<Ir.Expression>(),
    _ => throw new Rejection("Unknown statement in wrapper occurrence inventory", Bpl.Token.NoToken)
  };
  private static IEnumerable<Ir.Statement> StatementChildren(Ir.Statement statement) => statement switch {
    Ir.Block block => block.Statements, Ir.Choice choice => choice.Branches,
    Ir.Conditional conditional => new[] { conditional.Then, conditional.Else }, Ir.Loop loop => new[] { loop.Body }, Ir.Labeled labeled => new[] { labeled.Body },
    Ir.Assign or Ir.Check or Ir.Assume or Ir.Havoc or Ir.Exit or Ir.Return => Array.Empty<Ir.Statement>(),
    _ => throw new Rejection("Unknown statement in wrapper occurrence inventory", Bpl.Token.NoToken)
  };
  private static IEnumerable<Ir.Expression> ExpressionChildren(Ir.Expression expression) => expression switch {
    Ir.Application application => application.Arguments, Ir.Operation operation => operation.Arguments, Ir.BitvectorOperation operation => operation.Arguments,
    Ir.Quantifier quantifier => quantifier.Patterns.SelectMany(pattern => pattern).Append(quantifier.Body), Ir.Let let => new[] { let.Value, let.Body },
    Ir.Label label => new[] { label.Body }, Ir.BooleanLiteral or Ir.IntegerLiteral or Ir.RationalLiteral or Ir.BitvectorLiteral or Ir.Variable => Array.Empty<Ir.Expression>(),
    _ => throw new Rejection("Unknown expression in wrapper occurrence inventory", Bpl.Token.NoToken)
  };

  private static bool TryShape(Bpl.Axiom axiom, HashSet<Bpl.Function> owned, out Shape shape) {
    shape = null;
    if (axiom.GetType() != typeof(Bpl.Axiom) || axiom.CanHide || axiom.Attributes != null ||
        axiom.Expr is not Bpl.ForallExpr forall || forall.Type?.IsBool != true || forall.TypeParameters.Count != 0 ||
        forall.Attributes != null || forall.Dummies.Count != 1 || forall.Dummies[0] is not Bpl.BoundVariable binder ||
        binder.TypedIdent.WhereExpr != null || Width(binder.TypedIdent.Type) is not (> 0 and <= Ir.Protocol.MaximumBitvectorWidth) ||
        forall.Triggers is not { Pos: true, Next: null } trigger || trigger.Tr.Count() != 1 ||
        !Call(trigger.Tr.Single(), binder, out var wrapper) || !owned.Contains(wrapper) ||
        !Function(wrapper, binder, false)) { return false; }
    var width = Width(binder.TypedIdent.Type);
    if (!Binary(forall.Body, Bpl.BinaryOperator.Opcode.And, out var range, out var equality) ||
        !Binary(range, Bpl.BinaryOperator.Opcode.And, out var lower, out var upper) ||
        !Binary(lower, Bpl.BinaryOperator.Opcode.Le, out var zero, out var lowCall) ||
        !Integer(zero, BigInteger.Zero) || !SameCall(lowCall, binder, wrapper) ||
        !Binary(upper, Bpl.BinaryOperator.Opcode.Lt, out var upperCall, out var maximum) ||
        !SameCall(upperCall, binder, wrapper) || !Integer(maximum, BigInteger.One << width) ||
        !Binary(equality, Bpl.BinaryOperator.Opcode.Eq, out var defined, out var nativeCall) ||
        !SameCall(defined, binder, wrapper) || !Call(nativeCall, binder, out var native) ||
        ReferenceEquals(wrapper, native) || !owned.Contains(native) || !Function(native, binder, true) ||
        new HashSet<Bpl.Variable>(wrapper.InParams.Concat(wrapper.OutParams).Concat(native.InParams).Concat(native.OutParams),
          ReferenceEqualityComparer.Instance).Count != 4) { return false; }
    shape = new Shape(wrapper, native, axiom, binder, width, wrapper.InParams[0], wrapper.OutParams[0], native.InParams[0], native.OutParams[0]); return true;
  }
  private static bool Function(Bpl.Function function, Bpl.Variable binder, bool native) =>
    function.GetType() == typeof(Bpl.Function) && function.TypeParameters.Count == 0 && function.InParams.Count == 1 && function.OutParams.Count == 1 &&
    function.Body == null && function.DefinitionBody == null && !function.DefinitionAxioms.Any() &&
    function.InParams.Concat(function.OutParams).All(formal => formal.TypedIdent.WhereExpr == null) &&
    Width(function.InParams[0].TypedIdent.Type) == Width(binder.TypedIdent.Type) && function.OutParams[0].TypedIdent.Type.IsInt &&
    (native ? function.Attributes is { Key: "bvbuiltin", Params.Count: 1, Next: null } attribute && attribute.Params[0] is "bv2int"
      : function.Attributes == null);
  private static bool Monomorphic(Bpl.NAryExpr call) => call.TypeParameters == null || call.TypeParameters.FormalTypeParams.Count == 0;
  private static bool Call(Bpl.Expr expression, Bpl.Variable binder, out Bpl.Function function) {
    function = null;
    if (expression is not Bpl.NAryExpr { Fun: Bpl.FunctionCall call, Args.Count: 1 } application ||
        application.Type?.IsInt != true || !Monomorphic(application) || application.Args[0] is not Bpl.IdentifierExpr identifier ||
        !ReferenceEquals(identifier.Decl, binder) || Width(identifier.Type) != Width(binder.TypedIdent.Type)) { return false; }
    function = call.Func; return function != null;
  }
  private static bool SameCall(Bpl.Expr expression, Bpl.Variable binder, Bpl.Function function) =>
    Call(expression, binder, out var actual) && ReferenceEquals(actual, function);
  private static bool Binary(Bpl.Expr expression, Bpl.BinaryOperator.Opcode opcode, out Bpl.Expr left, out Bpl.Expr right) {
    left = right = null;
    if (expression is not Bpl.NAryExpr { Fun: Bpl.BinaryOperator operation, Args.Count: 2 } application ||
        operation.Op != opcode || application.Type?.IsBool != true || !Monomorphic(application)) { return false; }
    left = application.Args[0]; right = application.Args[1];
    return opcode == Bpl.BinaryOperator.Opcode.And ? left.Type?.IsBool == true && right.Type?.IsBool == true
      : left.Type?.IsInt == true && right.Type?.IsInt == true;
  }
  private static bool Integer(Bpl.Expr expression, BigInteger value) => expression is Bpl.LiteralExpr { Val: BigNum number } &&
    expression.Type?.IsInt == true && number.ToBigInteger == value;
  private static int Width(Bpl.Type type) {
    for (var depth = 0; depth < Ir.Protocol.MaximumDepth; depth++) {
      if (type is Bpl.TypeProxy proxy && ProxyTarget != null) { type = (Bpl.Type)ProxyTarget.GetValue(proxy); }
      else if (type is Bpl.TypeSynonymAnnotation alias) { type = alias.ExpandedType; }
      else { return type is Bpl.BvType word ? word.Bits : 0; }
    }
    return 0;
  }
  private static void Require(bool condition, string message, Bpl.IToken token) {
    if (!condition) { throw new Rejection(message, token); }
  }
}
