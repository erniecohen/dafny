// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.BaseTypes;
using Bpl = Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace Microsoft.Dafny;

#nullable enable
public sealed record B3NormalizationDiagnostic(string Code, string Message, Bpl.IToken Token);
public sealed record B3NormalizationResult(Ir.Program? Program, IReadOnlyList<Ir.SourceIdentity> Obligations,
  IReadOnlyList<B3NormalizationDiagnostic> Diagnostics, IReadOnlyList<string> Approximations) {
  public bool Success => Program != null && Diagnostics.Count == 0;
}

#nullable disable

/// <summary>
/// An owned, axiom-free overapproximation of a resolved, typed, structured pre-VC unit.
/// The input is observed, never desugared, passified, pruned, mutated or submitted to Boogie's engine.
/// </summary>
public static class B3Normalizer {
  public static B3NormalizationResult Normalize(Bpl.Program program, Bpl.Implementation implementation,
    DafnyOptions options) {
    try {
      return new Normalization(program, implementation, options).Run();
    } catch (Unsupported unsupported) {
      return new B3NormalizationResult(null, Array.Empty<Ir.SourceIdentity>(),
        new[] { new B3NormalizationDiagnostic(unsupported.Code, unsupported.Message, unsupported.Token) },
        Array.Empty<string>());
    }
  }

  private sealed class Unsupported : Exception {
    public readonly string Code;
    public readonly Bpl.IToken Token;
    public Unsupported(string code, string message, Bpl.IToken token) : base(message) {
      Code = code; Token = token;
    }
  }

  private sealed class Environment {
    public readonly Dictionary<Bpl.Variable, Ir.Expression> Values;
    public readonly Dictionary<Bpl.Variable, Ir.Expression> OldGlobals;
    public readonly bool OldFallbackIsCurrent;
    public Environment(Dictionary<Bpl.Variable, Ir.Expression> values,
      Dictionary<Bpl.Variable, Ir.Expression> oldGlobals, bool oldFallbackIsCurrent = false) {
      Values = values; OldGlobals = oldGlobals; OldFallbackIsCurrent = oldFallbackIsCurrent;
    }
    public Environment Bind(Dictionary<Bpl.Variable, Ir.Expression> values) =>
      new(new Dictionary<Bpl.Variable, Ir.Expression>(Values.Concat(values)
        .GroupBy(p => p.Key).Select(g => g.Last())), OldGlobals, OldFallbackIsCurrent);
  }

  private sealed class Normalization {
    // ProxyFor's getter shortens paths. Read its pinned layout to keep the source AST untouched.
    private static readonly FieldInfo ProxyTarget = typeof(Bpl.TypeProxy).GetField("proxyFor",
      BindingFlags.Instance | BindingFlags.NonPublic);
    private readonly Bpl.Program source;
    private readonly Bpl.Implementation unit;
    private readonly DafnyOptions options;
    private readonly List<Ir.Binding> variables = new();
    private readonly Dictionary<Bpl.Variable, Ir.Variable> names = new();
    private readonly Dictionary<Bpl.Variable, Ir.Variable> oldNames = new();
    private readonly Dictionary<string, Ir.Function> functions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> types = new(StringComparer.Ordinal);
    private readonly Dictionary<Bpl.Variable, (Bpl.Expr Expression, Environment Environment)> outputWhere = new();
    private readonly List<Ir.SourceIdentity> obligations = new();
    private readonly Dictionary<Bpl.LambdaExpr, string> lambdaNames = new();
    private int variableNumber;
    private int boundNumber;
    private int expressionCount;
    private Environment entry;

    public Normalization(Bpl.Program source, Bpl.Implementation unit, DafnyOptions options) {
      this.source = source; this.unit = unit; this.options = options;
    }

    public B3NormalizationResult Run() {
      Require(source.Implementations.Contains(unit) && unit.Proc != null, "b3_unit", "Unit is not a resolved implementation", unit.tok);
      Require(options.TypeEncodingMethod == Bpl.CoreOptions.TypeEncoding.Arguments && options.StratifiedInlining == 0,
        "b3_encoding", "Initial B3 normalization requires Arguments type encoding and no stratified inlining", unit.tok);
      Require(unit.TypeParameters.Count == 0 && unit.Proc.TypeParameters.Count == 0,
        "b3_polymorphic_unit", "Polymorphic implementations require specialization", unit.tok);
      ValidateAttributes(unit.Attributes, "implementation", unit.tok);
      ValidateAttributes(unit.Proc.Attributes, "procedure", unit.Proc.tok);
      Require(unit.Proc.GetType() == typeof(Bpl.Procedure), "b3_procedure_kind", "Yield/action procedure semantics are unsupported", unit.Proc.tok);
      Require(unit.StructuredStmts != null, "b3_structure", "A structured pre-VC body is required", unit.tok);
      Require(unit.InParams.Count == unit.Proc.InParams.Count && unit.OutParams.Count == unit.Proc.OutParams.Count,
        "b3_formals", "Implementation/procedure formal lists differ", unit.tok);
      var formals = new Dictionary<Bpl.Variable, Ir.Expression>();
      for (var i = 0; i < unit.InParams.Count; i++) { formals.Add(unit.Proc.InParams[i], Name(unit.InParams[i])); }
      for (var i = 0; i < unit.OutParams.Count; i++) { formals.Add(unit.Proc.OutParams[i], Name(unit.OutParams[i])); }
      foreach (var local in unit.LocVars) { Name(local); }
      entry = new Environment(formals, new Dictionary<Bpl.Variable, Ir.Expression>());
      var prologue = new List<Ir.Statement>();
      foreach (var global in source.TopLevelDeclarations.OfType<Bpl.GlobalVariable>()) {
        Where(global.TypedIdent.WhereExpr, entry, prologue);
      }
      foreach (var formal in unit.Proc.InParams.Concat(unit.Proc.OutParams)) {
        Where(formal.TypedIdent.WhereExpr, entry, prologue);
      }
      for (var i = 0; i < unit.OutParams.Count; i++) {
        var where = unit.Proc.OutParams[i].TypedIdent.WhereExpr;
        if (where != null) { outputWhere.Add(unit.OutParams[i], (where, entry)); }
      }
      foreach (var local in unit.LocVars) { Where(local.TypedIdent.WhereExpr, entry, prologue); }
      foreach (var requires in unit.Proc.Requires) {
        ValidateAttributes(requires.Attributes, "requires", requires.tok);
        prologue.Add(new Ir.Assume(Expr(requires.Condition, entry)));
      }
      var body = Structured(unit.StructuredStmts, entry);
      var exit = Exit(entry);
      // Discovering expressions above discovers every demanded global. Save entry state before any assumptions.
      var snapshots = oldNames.Select(pair => (Ir.Statement)new Ir.Assign(pair.Value.Name, Name(pair.Key))).ToList();
      var statements = snapshots.Concat(prologue).Append(body).Append(exit).ToArray();
      var normalized = new Ir.Program(types.Values.ToArray(), functions.Values.ToArray(), Array.Empty<Ir.Axiom>(),
        new Ir.Unit(Symbol("unit:" + unit.Name), variables.ToArray(), new Ir.Block(statements)));
      return new B3NormalizationResult(normalized, obligations.ToArray(), Array.Empty<B3NormalizationDiagnostic>(),
        new[] { "All source axioms, distinct-constant constraints, nonidentity function definitions, and map/lambda equations are omitted. Demanded closed function instances, constants and maps are uninterpreted; verification therefore uses a weaker context." });
    }

    private Ir.Variable Fresh(string type) {
      var binding = new Ir.Binding("sV" + ++variableNumber, type);
      variables.Add(binding);
      return new Ir.Variable(binding.Name, binding.Type);
    }
    private Ir.Variable Name(Bpl.Variable variable) {
      Require(variable != null, "b3_resolution", "Unresolved variable", unit.tok);
      Require(!Bpl.QKeyValueExtensions.FindBoolAttribute(variable.Attributes, "assumption"),
        "b3_assumption_variable", "Assumption-variable state semantics are unsupported", variable.tok);
      if (!names.TryGetValue(variable, out var result)) {
        result = Fresh(Type(variable.TypedIdent.Type)); names.Add(variable, result);
      }
      return result;
    }
    private Ir.Expression Variable(Bpl.Variable variable, Environment env, bool old) {
      if (env.Values.TryGetValue(variable, out var value)) { return value; }
      if (variable is Bpl.Constant constant) {
        return Apply("constant:" + constant.Name, Type(constant.TypedIdent.Type), Array.Empty<Ir.Expression>());
      }
      if (old && variable is Bpl.GlobalVariable) {
        if (env.OldGlobals.TryGetValue(variable, out value)) { return value; }
        if (env.OldFallbackIsCurrent) { return Name(variable); }
        if (!oldNames.TryGetValue(variable, out var snapshot)) {
          snapshot = Fresh(Type(variable.TypedIdent.Type)); oldNames.Add(variable, snapshot);
        }
        return snapshot;
      }
      return Name(variable);
    }
    private string Type(Bpl.Type type) {
      var key = TypeKey(type, new Dictionary<Bpl.TypeVariable, int>(), 0);
      if (key is "bool" or "int") { return key; }
      if (!types.TryGetValue(key, out var name)) { name = Symbol("type:" + key); types.Add(key, name); }
      return name;
    }
    private string TypeKey(Bpl.Type type, Dictionary<Bpl.TypeVariable, int> bound, int depth) {
      Require(type != null && depth < Ir.Protocol.MaximumDepth, "b3_type", "Missing or excessively nested type", unit.tok);
      if (type is Bpl.TypeProxy proxy) {
        Require(ProxyTarget != null, "b3_boogie_layout", "Pinned Boogie type-proxy layout changed", type.tok);
        var target = (Bpl.Type)ProxyTarget.GetValue(proxy);
        Require(target != null, "b3_type_proxy", "Unresolved type proxy", type.tok);
        return TypeKey(target, bound, depth + 1);
      }
      if (type is Bpl.TypeSynonymAnnotation alias) { return TypeKey(alias.ExpandedType, bound, depth + 1); }
      if (type is Bpl.BasicType basic) {
        Require(basic.IsBool || basic.IsInt, "b3_primitive_type", "Initial B3 slice supports bool and int primitives", type.tok);
        return basic.IsBool ? "bool" : "int";
      }
      if (type is Bpl.TypeVariable parameter) {
        Require(bound.TryGetValue(parameter, out var index), "b3_open_type", "Residual free type parameter", type.tok);
        return "bound" + index;
      }
      if (type is Bpl.CtorType ctor) {
        return "ctor(" + ctor.Decl.Name.Length + ":" + ctor.Decl.Name + ";" +
          string.Join(";", ctor.Arguments.Select(a => TypeKey(a, bound, depth + 1))) + ")";
      }
      if (type is Bpl.MapType map) {
        var nested = new Dictionary<Bpl.TypeVariable, int>(bound);
        foreach (var mapParameter in map.TypeParameters) { nested[mapParameter] = nested.Count; }
        return "map(" + map.TypeParameters.Count + ";" +
          string.Join(";", map.Arguments.Select(a => TypeKey(a, nested, depth + 1))) + ";" +
          TypeKey(map.Result, nested, depth + 1) + ")";
      }
      throw new Unsupported("b3_type", "Unsupported type " + type.GetType().Name, type.tok);
    }
    private Ir.Expression Apply(string key, string result, IReadOnlyList<Ir.Expression> args) {
      key += "(" + string.Join(",", args.Select(a => a.Type)) + ")->" + result;
      if (!functions.TryGetValue(key, out var function)) {
        function = new Ir.Function(Symbol("function:" + key), args.Select((a, i) => new Ir.Binding("sP" + i, a.Type)).ToArray(), result);
        functions.Add(key, function);
      }
      return new Ir.Application(function.Name, result, args);
    }
    private Ir.Expression Expr(Bpl.Expr expression, Environment env, bool old = false, int depth = 0) {
      Require(expression != null && depth < Ir.Protocol.MaximumDepth && ++expressionCount <= Ir.Protocol.MaximumNodes,
        "b3_expression_limit", "Missing expression or normalization resource bound exceeded", expression?.tok ?? unit.tok);
      var type = Type(expression.Type);
      switch (expression) {
        case Bpl.LiteralExpr literal when literal.Val is bool boolean: return new Ir.BooleanLiteral(boolean);
        case Bpl.LiteralExpr literal when literal.Val is BigNum integer: return new Ir.IntegerLiteral(integer.ToString());
        case Bpl.IdentifierExpr identifier: return Variable(identifier.Decl, env, old);
        case Bpl.OldExpr previous: return Expr(previous.Expr, env, true, depth + 1);
        case Bpl.NAryExpr application: {
          var args = application.Args.Select(a => Expr(a, env, old, depth + 1)).ToArray();
          switch (application.Fun) {
            case Bpl.UnaryOperator unary:
              return new Ir.Operation(unary.Op == Bpl.UnaryOperator.Opcode.Not ? Ir.Operator.Not : Ir.Operator.Negate, type, args);
            case Bpl.BinaryOperator binary: return Binary(binary.Op, type, args, expression.tok);
            case Bpl.IfThenElse: return new Ir.Operation(Ir.Operator.IfThenElse, type, args);
            case Bpl.TypeCoercion:
              Require(args.Length == 1 && args[0].Type == type, "b3_coercion", "Nontrivial type coercion is unsupported", expression.tok);
              return args[0];
            case Bpl.MapSelect: return Apply("map-select", type, args);
            case Bpl.MapStore: return Apply("map-store", type, args);
            case Bpl.FunctionCall call:
              Require(call.Func != null, "b3_resolution", "Unresolved function call", expression.tok);
              var projection = IdentityProjection(call.Func);
              if (projection >= 0 && projection < args.Length && args[projection].Type == type) { return args[projection]; }
              var instantiation = call.Func.TypeParameters.Count == 0 ? "" :
                string.Join(",", call.Func.TypeParameters.Select(p => {
                  Require(application.TypeParameters != null, "b3_instantiation", "Missing resolved function instantiation", expression.tok);
                  return Type(application.TypeParameters[p]);
                }));
              return Apply("function:" + call.Func.Name + "<" + instantiation + ">", type, args);
            default: throw new Unsupported("b3_operator", "Unsupported expression operator " + application.Fun.GetType().Name, expression.tok);
          }
        }
        case Bpl.QuantifierExpr quantifier: {
          Require(quantifier.TypeParameters.Count == 0 && quantifier.Dummies.Count > 0,
            "b3_quantifier", "Type-polymorphic or empty quantifiers require specialization", quantifier.tok);
          Require(quantifier.Dummies.All(v => v.TypedIdent.WhereExpr == null), "b3_bound_where", "Bound-variable where clauses are unsupported", quantifier.tok);
          var bindings = quantifier.Dummies.Select(v => new Ir.Binding("sB" + ++boundNumber, Type(v.TypedIdent.Type))).ToArray();
          var nested = env.Bind(quantifier.Dummies.Select((v, i) => new KeyValuePair<Bpl.Variable, Ir.Expression>(v,
            new Ir.Variable(bindings[i].Name, bindings[i].Type))).ToDictionary(p => p.Key, p => p.Value));
          var patterns = new List<IReadOnlyList<Ir.Expression>>();
          for (var trigger = quantifier.Triggers; trigger != null; trigger = trigger.Next) {
            if (trigger.Pos) { patterns.Add(trigger.Tr.Select(t => Expr(t, nested, old, depth + 1)).ToArray()); }
          }
          return new Ir.Quantifier(quantifier is Bpl.ForallExpr, bindings, patterns, Expr(quantifier.Body, nested, old, depth + 1));
        }
        case Bpl.LetExpr let: {
          var values = let.Rhss.Select(rhs => Expr(rhs, env, old, depth + 1)).ToArray();
          var bindings = let.Dummies.Select(v => new Ir.Binding("sB" + ++boundNumber, Type(v.TypedIdent.Type))).ToArray();
          var nested = env.Bind(let.Dummies.Select((v, i) => new KeyValuePair<Bpl.Variable, Ir.Expression>(v,
            new Ir.Variable(bindings[i].Name, bindings[i].Type))).ToDictionary(p => p.Key, p => p.Value));
          var body = Expr(let.Body, nested, old, depth + 1);
          for (var i = bindings.Length - 1; i >= 0; i--) { body = new Ir.Let(bindings[i], values[i], body); }
          return body;
        }
        case Bpl.LambdaExpr lambda: {
          Require(lambda.Dummies.All(v => v.TypedIdent.WhereExpr == null), "b3_bound_where", "Lambda bound-variable where clauses are unsupported", lambda.tok);
          if (!lambdaNames.TryGetValue(lambda, out var lambdaName)) {
            lambdaName = "lambda:" + lambdaNames.Count; lambdaNames.Add(lambda, lambdaName);
          }
          var captures = new List<(Bpl.Variable Variable, bool Old)>();
          FreeVariables(lambda, new HashSet<Bpl.Variable>(), captures, old);
          return Apply(lambdaName, type, captures.Select(v => Variable(v.Variable, env, v.Old)).ToArray());
        }
        default: throw new Unsupported("b3_expression", "Unsupported expression " + expression.GetType().Name, expression.tok);
      }
    }
    private static int IdentityProjection(Bpl.Function function) {
      if (function.Body is Bpl.IdentifierExpr identifier) { return function.InParams.IndexOf(identifier.Decl); }
      // A universally quantified direct defining equality justifies this rewrite; an :identity claim alone does not.
      var axiom = function.DefinitionAxiom?.Expr;
      if (axiom is Bpl.ForallExpr forall && forall.Body is Bpl.NAryExpr equality &&
          equality.Fun is Bpl.BinaryOperator { Op: Bpl.BinaryOperator.Opcode.Eq }) {
        for (var side = 0; side < 2; side++) {
          var defined = equality.Args[side];
          if (defined is Bpl.NAryExpr { Fun: Bpl.TypeCoercion } coercion) { defined = coercion.Args[0]; }
          if (defined is Bpl.NAryExpr call && call.Fun is Bpl.FunctionCall fc && fc.Func == function &&
              equality.Args[1 - side] is Bpl.IdentifierExpr projected &&
              call.Args.Count == forall.Dummies.Count && call.Args.Select((a, i) => a is Bpl.IdentifierExpr id && id.Decl == forall.Dummies[i]).All(b => b)) {
            return forall.Dummies.IndexOf(projected.Decl);
          }
        }
      }
      return -1;
    }
    private static void FreeVariables(Bpl.Expr expression, HashSet<Bpl.Variable> bound, List<(Bpl.Variable Variable, bool Old)> found, bool old) {
      switch (expression) {
        case Bpl.IdentifierExpr identifier:
          if (!bound.Contains(identifier.Decl) && !found.Contains((identifier.Decl, old))) { found.Add((identifier.Decl, old)); }
          break;
        case Bpl.NAryExpr nary: foreach (var argument in nary.Args) { FreeVariables(argument, bound, found, old); } break;
        case Bpl.OldExpr previous: FreeVariables(previous.Expr, bound, found, true); break;
        case Bpl.BinderExpr binder:
          var nested = new HashSet<Bpl.Variable>(bound.Concat(binder.Dummies));
          FreeVariables(binder.Body, nested, found, old);
          break;
        case Bpl.LetExpr let:
          foreach (var rhs in let.Rhss) { FreeVariables(rhs, bound, found, old); }
          FreeVariables(let.Body, new HashSet<Bpl.Variable>(bound.Concat(let.Dummies)), found, old);
          break;
        case Bpl.LiteralExpr: break;
        default: throw new Unsupported("b3_lambda_capture", "Unsupported lambda capture expression " + expression.GetType().Name, expression.tok);
      }
    }
    private static Ir.Expression Binary(Bpl.BinaryOperator.Opcode op, string type, Ir.Expression[] args, Bpl.IToken token) {
      var kind = op switch {
        Bpl.BinaryOperator.Opcode.Add => Ir.Operator.Add, Bpl.BinaryOperator.Opcode.Sub => Ir.Operator.Subtract,
        Bpl.BinaryOperator.Opcode.Mul => Ir.Operator.Multiply, Bpl.BinaryOperator.Opcode.Eq => Ir.Operator.Equal,
        Bpl.BinaryOperator.Opcode.Neq => Ir.Operator.NotEqual, Bpl.BinaryOperator.Opcode.Lt or Bpl.BinaryOperator.Opcode.Gt => Ir.Operator.Less,
        Bpl.BinaryOperator.Opcode.Le or Bpl.BinaryOperator.Opcode.Ge => Ir.Operator.LessEqual,
        Bpl.BinaryOperator.Opcode.And => Ir.Operator.And, Bpl.BinaryOperator.Opcode.Or => Ir.Operator.Or,
        Bpl.BinaryOperator.Opcode.Imp => Ir.Operator.Implies, Bpl.BinaryOperator.Opcode.Iff => Ir.Operator.Equiv,
        _ => throw new Unsupported("b3_arithmetic", "Division, modulo, real/float division and power are unsupported pending correspondence", token)
      };
      if (op is Bpl.BinaryOperator.Opcode.Gt or Bpl.BinaryOperator.Opcode.Ge) { args = new[] { args[1], args[0] }; }
      return new Ir.Operation(kind, type, args);
    }
    private void Where(Bpl.Expr where, Environment env, List<Ir.Statement> statements) {
      if (where != null) { statements.Add(new Ir.Assume(Expr(where, env))); }
    }
    private Ir.Check Check(Bpl.Expr condition, Environment env, Bpl.IToken token, string description, Bpl.QKeyValue attributes) {
      var expression = Expr(condition, env);
      var subsumption = Bpl.QKeyValue.FindIntAttribute(attributes, "subsumption", -1);
      var mode = subsumption switch { 0 => Bpl.CoreOptions.SubsumptionOption.Never,
        1 => Bpl.CoreOptions.SubsumptionOption.NotForQuantifiers, 2 => Bpl.CoreOptions.SubsumptionOption.Always,
        _ => options.UseSubsumption };
      var learn = mode == Bpl.CoreOptions.SubsumptionOption.Always ||
        mode == Bpl.CoreOptions.SubsumptionOption.NotForQuantifiers && expression is not Ir.Quantifier;
      var id = "sO" + (obligations.Count + 1);
      var origin = BoogieGenerator.ToDafnyToken(token);
      obligations.Add(new Ir.SourceIdentity(id, origin.Uri?.AbsoluteUri ?? token.filename ?? "",
        Math.Max(0, token.line), Math.Max(0, token.col), description));
      return new Ir.Check(id, expression, learn);
    }
    private Ir.Statement Exit(Environment env) {
      var statements = new List<Ir.Statement>();
      foreach (var ensures in unit.Proc.Ensures) {
        ValidateAttributes(ensures.Attributes, "ensures", ensures.tok);
        if (!ensures.Free) { statements.Add(Check(ensures.Condition, env, ensures.tok,
          ensures.Description?.FailureDescription ?? "postcondition", null)); }
        else if (ensures.CanAlwaysAssume()) { statements.Add(new Ir.Assume(Expr(ensures.Condition, env))); }
      }
      statements.Add(new Ir.Return());
      return new Ir.Block(statements.ToArray());
    }
    private Ir.Statement Structured(Bpl.StmtList list, Environment env, int depth = 0) {
      Require(depth < Ir.Protocol.MaximumDepth, "b3_statement_limit", "Structured nesting exceeds normalization bound", list.EndCurly);
      var statements = new List<Ir.Statement>();
      if (list.PrefixCommands != null) { statements.AddRange(list.PrefixCommands.Select(c => Command(c, env))); }
      foreach (var block in list.BigBlocks) {
        statements.AddRange(block.simpleCmds.Select(c => Command(c, env)));
        if (block.ec != null) { statements.Add(StructuredCommand(block.ec, env, depth + 1)); }
        else if (block.tc is Bpl.ReturnCmd and not Bpl.ReturnExprCmd) { statements.Add(Exit(env)); }
        else if (block.tc != null) { throw new Unsupported("b3_transfer", "Explicit goto or return-expression control is unsupported", block.tc.tok); }
      }
      return new Ir.Block(statements.ToArray());
    }
    private Ir.Statement StructuredCommand(Bpl.StructuredCmd command, Environment env, int depth) {
      if (command is Bpl.IfCmd conditional) {
        ValidateAttributes(conditional.Attributes, "conditional", conditional.tok);
        var then = Structured(conditional.Thn, env, depth);
        var other = conditional.ElseIf != null ? StructuredCommand(conditional.ElseIf, env, depth + 1) :
          conditional.ElseBlock != null ? Structured(conditional.ElseBlock, env, depth) : new Ir.Block(Array.Empty<Ir.Statement>());
        return conditional.Guard == null ? new Ir.Choice(new[] { then, other }) :
          new Ir.Conditional(Expr(conditional.Guard, env), then, other);
      }
      throw new Unsupported("b3_structured_control", "Loops and breaks require explicit invariant/control lowering", command.tok);
    }
    private Ir.Statement Command(Bpl.Cmd command, Environment env) {
      if (command is Bpl.ICarriesAttributes attributed) { ValidateAttributes(attributed.Attributes, "command", command.tok); }
      switch (command) {
        case Bpl.CommentCmd: return new Ir.Block(Array.Empty<Ir.Statement>());
        case Bpl.AssertCmd assertion: return Check(assertion.Expr, env, assertion.tok,
          assertion.Description?.FailureDescription ?? "assertion", assertion.Attributes);
        case Bpl.AssumeCmd assumption: return new Ir.Assume(Expr(assumption.Expr, env));
        case Bpl.AssignCmd assignment: {
          Require(assignment.Lhss.All(lhs => lhs is Bpl.SimpleAssignLhs), "b3_map_assignment",
            "Map assignment requires a separate store-equation lowering", assignment.tok);
          var values = assignment.Rhss.Select(rhs => Expr(rhs, env)).ToArray();
          var temporaries = values.Select(value => Fresh(value.Type)).ToArray();
          return new Ir.Block(values.Select((value, i) => (Ir.Statement)new Ir.Assign(temporaries[i].Name, value))
            .Concat(assignment.Lhss.Select((lhs, i) => (Ir.Statement)new Ir.Assign(Name(lhs.DeepAssignedVariable).Name, temporaries[i]))).ToArray());
        }
        case Bpl.HavocCmd havoc: return Havoc(havoc.Vars.Select(v => v.Decl).ToArray(), env);
        case Bpl.StateCmd state: {
          var statements = new List<Ir.Statement> { new Ir.Havoc(state.Locals.Select(v => Name(v).Name).ToArray()) };
          foreach (var local in state.Locals) { Where(local.TypedIdent.WhereExpr, env, statements); }
          statements.AddRange(state.Cmds.Select(c => Command(c, env)));
          return new Ir.Block(statements.ToArray());
        }
        case Bpl.CallCmd call: return Call(call, env);
        case Bpl.HideRevealCmd or Bpl.ChangeScope:
          throw new Unsupported("b3_visibility", "Hide/reveal and visibility scopes require pinned pruning correspondence", command.tok);
        default: throw new Unsupported("b3_command", "Unsupported command " + command.GetType().Name, command.tok);
      }
    }
    private Ir.Statement Havoc(IReadOnlyList<Bpl.Variable> changed, Environment env) {
      var statements = new List<Ir.Statement> { new Ir.Havoc(changed.Select(v => Name(v).Name).Distinct().ToArray()) };
      foreach (var variable in changed.Distinct()) {
        if (outputWhere.TryGetValue(variable, out var where)) { Where(where.Expression, where.Environment, statements); }
        else { Where(variable.TypedIdent.WhereExpr, env, statements); }
      }
      return new Ir.Block(statements.ToArray());
    }
    private Ir.Statement Call(Bpl.CallCmd call, Environment caller) {
      ValidateAttributes(call.Proc?.Attributes, "callee", call.tok);
      Require(call.Proc != null && call.Proc.GetType() == typeof(Bpl.Procedure) && call.Proc.TypeParameters.Count == 0 && !call.IsAsync &&
        call.AssignedAssumptionVariable == null && call.Ins.All(a => a != null) && call.Outs.All(a => a != null),
        "b3_call", "Polymorphic/async calls, assumption-variable calls and wildcard arguments require additional lowering", call.tok);
      var statements = new List<Ir.Statement>();
      var substitutions = new Dictionary<Bpl.Variable, Ir.Expression>();
      for (var i = 0; i < call.Ins.Count; i++) {
        var value = Expr(call.Ins[i], caller); var saved = Fresh(value.Type);
        statements.Add(new Ir.Assign(saved.Name, value)); substitutions.Add(call.Proc.InParams[i], saved);
      }
      var callOld = new Dictionary<Bpl.Variable, Ir.Expression>();
      foreach (var modified in call.Proc.Modifies.Select(m => m.Decl).Distinct()) {
        var current = Name(modified); var saved = Fresh(current.Type);
        statements.Add(new Ir.Assign(saved.Name, current)); callOld.Add(modified, saved);
      }
      var outputs = call.Proc.OutParams.Select(p => Fresh(Type(p.TypedIdent.Type))).ToArray();
      for (var i = 0; i < outputs.Length; i++) { substitutions.Add(call.Proc.OutParams[i], outputs[i]); }
      // Unmodified globals in an ensures-old expression denote current pre/post-call values.
      var callee = new Environment(substitutions, callOld, oldFallbackIsCurrent: true);
      var requirementAndWhere = new Environment(substitutions, caller.OldGlobals);
      // CallCmd's temporary output where clauses already exist when its StateCmd is entered.
      // They are assumed with fresh input/output temporaries before evaluating/saving actual inputs,
      // then assumed again after the joint output/frame havoc below.
      var callEntry = new List<Ir.Statement>();
      var temporaryNames = substitutions.Values.Cast<Ir.Variable>().Select(v => v.Name).ToArray();
      if (temporaryNames.Length > 0) { callEntry.Add(new Ir.Havoc(temporaryNames)); }
      foreach (var output in call.Proc.OutParams) { Where(output.TypedIdent.WhereExpr, requirementAndWhere, callEntry); }
      statements.InsertRange(0, callEntry);
      foreach (var requires in call.Proc.Requires) {
        ValidateAttributes(requires.Attributes, "callee requires", requires.tok);
        if (!requires.Free && !call.IsFree) { statements.Add(Check(requires.Condition, requirementAndWhere, call.tok,
          requires.Description?.FailureDescription ?? "call precondition", call.Attributes)); }
        else if (requires.CanAlwaysAssume()) { statements.Add(new Ir.Assume(Expr(requires.Condition, requirementAndWhere))); }
      }
      var modifiedGlobals = call.Proc.Modifies.Select(m => m.Decl).Distinct().ToArray();
      statements.Add(new Ir.Havoc(modifiedGlobals.Select(v => Name(v).Name).Concat(outputs.Select(v => v.Name)).ToArray()));
      foreach (var global in modifiedGlobals) { Where(global.TypedIdent.WhereExpr, caller, statements); }
      foreach (var output in call.Proc.OutParams) { Where(output.TypedIdent.WhereExpr, requirementAndWhere, statements); }
      foreach (var ensures in call.Proc.Ensures) {
        ValidateAttributes(ensures.Attributes, "callee ensures", ensures.tok);
        statements.Add(new Ir.Assume(Expr(ensures.Condition, callee)));
      }
      for (var i = 0; i < call.Outs.Count; i++) { statements.Add(new Ir.Assign(Name(call.Outs[i].Decl).Name, outputs[i])); }
      return new Ir.Block(statements.ToArray());
    }
    private static void ValidateAttributes(Bpl.QKeyValue attributes, string context, Bpl.IToken token) {
      for (var attribute = attributes; attribute != null; attribute = attribute.Next) {
        if (attribute.Key == "smt_option") {
          Require(attribute.Params.Count == 2 && attribute.Params[0] is string name && name == "smt.arith.solver" &&
            attribute.Params[1] is string value && value == "2", "b3_solver_attribute",
            "Only the generated arithmetic-solver 2 SMT override is supported", token);
          continue;
        }
        Require(attribute.Key is "id" or "verboseName" or "checksum" or "verify" or "priority" or
          "msg" or "subsumption" or "partition" or "captureState" or "always_assume" or
          "allow_path_isolation", "b3_attribute", "Unsupported " + context + " attribute :" + attribute.Key, token);
      }
    }
    private static string Symbol(string text) => "s" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static void Require(bool condition, string code, string message, Bpl.IToken token) {
      if (!condition) { throw new Unsupported(code, message, token); }
    }
  }
}
