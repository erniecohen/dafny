// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Numerics;
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
  IReadOnlyList<B3NormalizationDiagnostic> Diagnostics, IReadOnlyList<string> Approximations,
  IReadOnlyList<B3VerificationContext>? Contexts = null) {
  public bool Success => Program != null && Diagnostics.Count == 0;
}

#nullable disable

/// <summary>
/// An owned conservative normalization of a resolved, typed, structured pre-VC unit.
/// The input is observed, never desugared, passified, pruned, mutated or submitted to Boogie's engine.
/// </summary>
public static class B3Normalizer {
  public static B3NormalizationResult Normalize(Bpl.Program program, Bpl.Implementation implementation,
    DafnyOptions options) {
    try {
      return new Normalization(program, implementation, options).Run();
    } catch (B3StructuredCfgCorrespondence.Rejection rejected) {
      return new B3NormalizationResult(null, Array.Empty<Ir.SourceIdentity>(),
        new[] { new B3NormalizationDiagnostic("b3_cfg_correspondence", rejected.Message, rejected.Token) }, Array.Empty<string>());
    } catch (B3DefinitionVisibility.Rejection rejected) {
      return new B3NormalizationResult(null, Array.Empty<Ir.SourceIdentity>(),
        new[] { new B3NormalizationDiagnostic("b3_visibility", rejected.Message, rejected.Token) }, Array.Empty<string>());
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
    private readonly Dictionary<string, string> mapHelperOrigins = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> mapStoreNames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> mapObservationOrigins = new(StringComparer.Ordinal);
    private readonly HashSet<string> opaqueMapOrigins = new(StringComparer.Ordinal);
    private int mapReadEstimatedNodes;
    private readonly Dictionary<Bpl.Variable, (Bpl.Expr Expression, Environment Environment)> outputWhere = new();
    private readonly List<Ir.SourceIdentity> obligations = new();
    private readonly Dictionary<Bpl.LambdaExpr, string> lambdaNames = new();
    private readonly HashSet<string> jumpTargets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> jumpLabels = new(StringComparer.Ordinal);
    private int controlNumber;
    private int statementCount;
    private int variableNumber;
    private int boundNumber;
    private int expressionCount;
    private long numericCharacters;
    private Environment entry;
    private B3DefinitionVisibility visibility;
    private readonly List<Bpl.HideRevealCmd> visibilityCommands = new();
    private readonly HashSet<Bpl.ReturnCmd> explicitReturns = new();
    private readonly Dictionary<string, Bpl.Function> functionOwners = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (B3DefinitionVisibility.Frame Frame, Ir.Expression Condition, Bpl.Absy Origin)> checkMasks = new(StringComparer.Ordinal);

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
      B3StructuredCfgCorrespondence.Validate(unit);
      InspectControl(unit.StructuredStmts);
      visibility = new B3DefinitionVisibility(unit);
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
      var body = Structured(unit.StructuredStmts, entry, new Control());
      var exit = Exit(entry, null);
      ValidateRawAssertionCoverage();
      // Discovering expressions above discovers every demanded global. Save entry state before any assumptions.
      var snapshots = oldNames.Select(pair => (Ir.Statement)new Ir.Assign(pair.Value.Name, Name(pair.Key))).ToList();
      var statements = snapshots.Concat(prologue).Append(body).Append(exit).ToArray();
      var normalized = new Ir.Program(types.Values.ToArray(), functions.Values.ToArray(), Array.Empty<Ir.Axiom>(),
        new Ir.Unit(Symbol("unit:" + unit.Name), variables.ToArray(), new Ir.Block(statements)));
      var catalogue = LiteralDefinitions();
      // Formula conversion can demand additional stable guard symbols. Use the same declarations in every replay.
      normalized = normalized with { Types = types.Values.ToArray(), Functions = functions.Values.ToArray() };
      CheckOwnedBounds(normalized);
      var selected = SelectDefinitions(catalogue);
      var contexts = B3DefinitionContexts.Create(normalized, obligations.ToArray(), selected, unit.tok);
      return new B3NormalizationResult(normalized, obligations.ToArray(), Array.Empty<B3NormalizationDiagnostic>(),
        new[] { "Outside reviewed guarded literal-definition contexts, source axioms, distinct-constant constraints and lambda equations are omitted. Ordinary nonidentity function definitions are omitted; exact typed integer division/modulo bodies and active always-revealed universal definitions substitute their native operations. Other demanded closed function instances and constants are uninterpreted. Direct reads of owned closed monomorphic store expressions use the read-over-write ITE identity; other map operations remain uninterpreted. Exact metadata-free complete-tuple forall read equalities are abstracted by an uninterpreted Bool predicate of their two map values. No global read-over-write or observation axioms are asserted. Map equality remains opaque without extensionality.",
          "StateCmd and call-temporary scope-entry where predicates are omitted: pinned scope passification appends raw predicates without current-incarnation substitution. Post-havoc where predicates are preserved." }
          .Concat(mapHelperOrigins.Values).Concat(mapObservationOrigins.Values)
          .Concat(opaqueMapOrigins.OrderBy(s => s, StringComparer.Ordinal))
          .Concat(contexts.SelectMany(context => context.Definitions).DistinctBy(definition => definition.Id)
            .Select(definition => "Active source definition instance: owner=" + definition.Owner + "; source-axiom=" + definition.AxiomOrdinal +
              "; formula-sha256=" + definition.FormulaHash + "; instance=" + definition.Instance))
          .ToArray(), contexts);
    }

    private Bpl.ReturnCmd[] FallthroughReturns() => unit.Blocks.Select(block => block.TransferCmd).OfType<Bpl.ReturnCmd>()
      .Where(returned => !explicitReturns.Contains(returned) && (visibility == null || visibility.IsReachable(returned))).ToArray();

    private B3DefinitionVisibility.Frame FallthroughMask() {
      var returns = FallthroughReturns();
      if (returns.Length == 0) {
        // All source fallthroughs are absent. This appended static exit is unreachable;
        // retain its checks but add no definition premise for that synthetic position.
        return new B3DefinitionVisibility.Frame(Bpl.HideRevealCmd.Modes.Hide,
          System.Collections.Immutable.ImmutableHashSet<Bpl.Function>.Empty);
      }
      return returns.Select(returned => visibility.After(returned)).Aggregate(B3DefinitionVisibility.MergeFrames);
    }

    private sealed record OwnedFormula(Bpl.Function Owner, B3DefinitionContexts.Formula Formula);
    private IReadOnlyList<OwnedFormula> LiteralDefinitions() {
      var owners = functionOwners.Values.Concat(visibilityCommands.Where(command => command.Function != null)
        .Select(command => command.Function)).Distinct().ToArray();
      var catalogue = new List<OwnedFormula>();
      foreach (var function in owners) {
        if (!source.TopLevelDeclarations.Contains(function) || function.TypeParameters.Count != 0 ||
            function.InParams.Count > 1 || function.InParams.Any(parameter => !parameter.TypedIdent.Type.IsBool || parameter.TypedIdent.WhereExpr != null) ||
            function.OutParams.Count != 1 || !(function.OutParams[0].TypedIdent.Type.IsBool || function.OutParams[0].TypedIdent.Type.IsInt)) { continue; }
        foreach (var axiom in function.DefinitionAxioms) {
          if (!axiom.CanHide || !source.TopLevelDeclarations.Contains(axiom)) { continue; }
          var body = axiom.Expr;
          var binders = Array.Empty<Bpl.Variable>();
          if (body is Bpl.ForallExpr quantified) {
            if (quantified.TypeParameters.Count != 0 || quantified.Dummies.Count is < 1 or > 4 ||
                quantified.Dummies.Any(dummy => !dummy.TypedIdent.Type.IsBool || dummy.TypedIdent.WhereExpr != null)) { continue; }
            binders = quantified.Dummies.ToArray(); body = quantified.Body;
          }
          if (!GroundDefinition(body, binders.ToHashSet()) || !HasLiteralEquation(body, function, 0)) { continue; }
          for (var instance = 0; instance < 1 << binders.Length; instance++) {
            var values = binders.Select((binder, index) => new KeyValuePair<Bpl.Variable, Ir.Expression>(binder,
              new Ir.BooleanLiteral((instance & (1 << index)) != 0))).ToDictionary(pair => pair.Key, pair => pair.Value);
            var environment = new Environment(values, new Dictionary<Bpl.Variable, Ir.Expression>());
            var condition = Expr(body, environment);
            var ordinal = source.TopLevelDeclarations.ToList().IndexOf(axiom);
            var hash = B3DefinitionContexts.FormulaHash(condition);
            var instanceName = binders.Length == 0 ? "ground" : "bool-tuple-" + instance;
            var origin = new B3DefinitionOrigin(Symbol(function.Name + ":" + ordinal + ":" + instanceName + ":" + hash),
              function.Name, ordinal, hash, instanceName, Math.Max(0, axiom.tok.line), Math.Max(0, axiom.tok.col));
            catalogue.Add(new OwnedFormula(function, new B3DefinitionContexts.Formula(origin, condition)));
            Require(catalogue.Count <= 64, "b3_definition_limit", "Eligible definition instances exceed the bounded catalogue", axiom.tok);
          }
        }
      }
      foreach (var command in visibilityCommands.Where(command => command.Function != null)) {
        Require(catalogue.Any(formula => ReferenceEquals(formula.Owner, command.Function)), "b3_visibility",
          "Named hide/reveal requires an active owned guarded literal definition in the initial slice", command.tok);
      }
      if (visibilityCommands.Any(command => command.Function == null)) {
        Require(catalogue.Count > 0, "b3_visibility", "Wildcard visibility requires a demanded eligible source definition", unit.tok);
      }
      return catalogue;
    }

    private bool GroundDefinition(Bpl.Expr root, HashSet<Bpl.Variable> bound) {
      var pending = new Stack<(Bpl.Expr Expr, int Depth)>(); pending.Push((root, 0)); var count = 0;
      while (pending.Count > 0) {
        var (expression, depth) = pending.Pop();
        if (depth >= Ir.Protocol.MaximumDepth || ++count > Ir.Protocol.MaximumNodes) { return false; }
        switch (expression) {
          case Bpl.LiteralExpr literal when literal.Val is bool or BigNum: break;
          case Bpl.IdentifierExpr identifier when bound.Contains(identifier.Decl): break;
          case Bpl.IdentifierExpr { Decl: Bpl.Constant constant } when source.TopLevelDeclarations.Contains(constant) &&
              (constant.TypedIdent.Type.IsBool || constant.TypedIdent.Type.IsInt): break;
          case Bpl.NAryExpr application:
            if (application.Fun is Bpl.FunctionCall call) {
              if (call.Func == null) { return false; }
              var projection = IdentityProjection(call.Func);
              if (projection >= 0 && projection < application.Args.Count &&
                  (application.Type?.IsBool == true || application.Type?.IsInt == true) &&
                  (application.Type.IsBool && application.Args[projection].Type?.IsBool == true ||
                    application.Type.IsInt && application.Args[projection].Type?.IsInt == true)) {
                // This is the same source-backed identity substitution used by Expr.
                // In particular, resolved Lit<bool>(true) retains the whole owned formula.
                foreach (var argument in application.Args) { pending.Push((argument, depth + 1)); }
                break;
              }
              if (call.Func.TypeParameters.Count != 0 ||
                  !source.TopLevelDeclarations.Contains(call.Func) ||
                  call.Func.InParams.Any(parameter => !(parameter.TypedIdent.Type.IsBool || parameter.TypedIdent.Type.IsInt)) ||
                  call.Func.OutParams.Count != 1 || !(call.Func.OutParams[0].TypedIdent.Type.IsBool || call.Func.OutParams[0].TypedIdent.Type.IsInt)) { return false; }
            } else if (application.Fun is not (Bpl.BinaryOperator or Bpl.UnaryOperator or Bpl.IfThenElse or Bpl.TypeCoercion)) { return false; }
            foreach (var argument in application.Args) { pending.Push((argument, depth + 1)); }
            break;
          default: return false;
        }
      }
      return true;
    }

    private bool HasLiteralEquation(Bpl.Expr expression, Bpl.Function owner, int depth) {
      if (depth >= Ir.Protocol.MaximumDepth || expression is not Bpl.NAryExpr { Fun: Bpl.BinaryOperator binary } application) { return false; }
      if (binary.Op == Bpl.BinaryOperator.Opcode.Imp && application.Args.Count == 2) {
        return HasLiteralEquation(application.Args[1], owner, depth + 1);
      }
      if (binary.Op == Bpl.BinaryOperator.Opcode.And && application.Args.Count == 2) {
        return application.Args.Any(argument => HasLiteralEquation(argument, owner, depth + 1));
      }
      return application.Args.Count == 2 &&
        (binary.Op == Bpl.BinaryOperator.Opcode.Eq || binary.Op == Bpl.BinaryOperator.Opcode.Iff &&
          Type(application.Args[0].Type) == "bool" && Type(application.Args[1].Type) == "bool") &&
        application.Args[0] is Bpl.NAryExpr { Fun: Bpl.FunctionCall call } defining &&
        ReferenceEquals(call.Func, owner) && IsMonomorphic(defining) && defining.Args.Count == owner.InParams.Count &&
        (owner.InParams.Count == 0 || defining.Args[0] is Bpl.LiteralExpr { Val: true }) &&
        IsLiteralDefinitionValue(application.Args[1], 0);
    }
    private bool IsLiteralDefinitionValue(Bpl.Expr expression, int depth) {
      if (depth >= Ir.Protocol.MaximumDepth) { return false; }
      if (expression is Bpl.LiteralExpr literal) { return literal.Val is bool or BigNum; }
      if (expression is Bpl.NAryExpr { Fun: Bpl.FunctionCall call } application && call.Func != null) {
        var projection = IdentityProjection(call.Func);
        return projection >= 0 && projection < application.Args.Count && IsLiteralDefinitionValue(application.Args[projection], depth + 1);
      }
      return false;
    }

    private IReadOnlyDictionary<string, IReadOnlyList<B3DefinitionContexts.Formula>> SelectDefinitions(IReadOnlyList<OwnedFormula> catalogue) {
      var result = new Dictionary<string, IReadOnlyList<B3DefinitionContexts.Formula>>(StringComparer.Ordinal);
      var must = visibility?.AnalyzeMust(catalogue.Select(formula => formula.Owner).Distinct().ToArray());
      B3DefinitionVisibility.MustFrame Mask(Bpl.Absy origin) {
        if (origin != null) { return must.Before(origin); }
        var returns = FallthroughReturns();
        return returns.Length == 0 ? B3DefinitionVisibility.MustFrame.Unreachable :
          returns.Select(returned => must.After(returned)).Aggregate(B3DefinitionVisibility.MustFrame.Merge);
      }
      var mayRevealOperands = must == null ? Array.Empty<B3DefinitionVisibility.MustFrame>() :
        must.NativeAssertionOperands().Concat(checkMasks.Values.Select(check => Mask(check.Origin)))
          .Where(frame => frame.MayReveal).ToArray();
      foreach (var (id, check) in checkMasks) {
        if (visibility != null && (check.Origin == null ? FallthroughReturns().Length == 0 : !visibility.IsReachable(check.Origin))) {
          // A known disconnected source goal remains in the static partition without any definition premise.
          result.Add(id, Array.Empty<B3DefinitionContexts.Formula>());
          continue;
        }
        var demanded = new HashSet<Bpl.Function>();
        var pending = new Stack<Ir.Expression>(); pending.Push(check.Condition);
        while (pending.Count > 0) {
          var expression = pending.Pop();
          if (expression is Ir.Application application && functionOwners.TryGetValue(application.Name, out var owner)) { demanded.Add(owner); }
          foreach (var child in ExpressionChildren(expression)) { pending.Push(child); }
        }
        if (visibilityCommands.Count > 0) {
          foreach (var demandedOwner in demanded.Where(owner => owner.DefinitionAxioms.Any(axiom => axiom.CanHide))) {
            Require(catalogue.Any(formula => ReferenceEquals(formula.Owner, demandedOwner)), "b3_visibility",
              "Demanded visibility-sensitive definition is outside the guarded literal slice", demandedOwner.tok);
          }
        }
        var pathMask = must == null ? null : Mask(check.Origin);
        result.Add(id, catalogue.Where(formula => demanded.Contains(formula.Owner) && check.Frame.IsRevealed(formula.Owner) &&
          // A native path split can remove predecessors and change the exact raw merge's mode.
          // Preserve a sufficient owner premise under every retained path subset and mixed aggregate.
          (formula.Owner.AlwaysRevealed || pathMask == null || pathMask.IsRevealed(formula.Owner) &&
            (pathMask.AllReveal || mayRevealOperands.All(frame => frame.IsRevealed(formula.Owner)))))
          .Select(formula => formula.Formula).DistinctBy(formula => formula.Origin.Id).ToArray());
      }
      return result;
    }
    private static IEnumerable<Ir.Expression> ExpressionChildren(Ir.Expression expression) => expression switch {
      Ir.Application application => application.Arguments, Ir.Operation operation => operation.Arguments,
      Ir.Quantifier quantifier => new[] { quantifier.Body }.Concat(quantifier.Patterns.SelectMany(pattern => pattern)),
      Ir.Let let => new[] { let.Value, let.Body }, Ir.Label label => new[] { label.Body },
      _ => Array.Empty<Ir.Expression>()
    };

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
        Require(source.TopLevelDeclarations.Contains(constant), "b3_declaration_identity",
          "Opaque constant is not an active declaration in the typed source artifact", constant.tok);
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
      if (key is "bool" or "int" or "real") { return key; }
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
        Require(basic.IsBool || basic.IsInt || basic.IsReal, "b3_primitive_type", "B3 supports bool, int, and real primitives", type.tok);
        return basic.IsBool ? "bool" : basic.IsInt ? "int" : "real";
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
    private Ir.Expression MapOperation(Bpl.NAryExpr sourceApplication, string resultType, Ir.Expression[] arguments, bool store) {
      var head = sourceApplication.Args[0].Type;
      for (var depth = 0; depth < Ir.Protocol.MaximumDepth; depth++) {
        if (head is Bpl.TypeProxy proxy && ProxyTarget != null) { head = (Bpl.Type)ProxyTarget.GetValue(proxy); }
        else if (head is Bpl.TypeSynonymAnnotation alias) { head = alias.ExpandedType; }
        else { break; }
      }
      Require(head is Bpl.MapType, "b3_map_type", "Map operation requires a resolved map type", sourceApplication.tok);
      var map = (Bpl.MapType)head;
      var mapSort = Type(map);
      if (map.TypeParameters.Count != 0) {
        opaqueMapOrigins.Add("Polymorphic map sort " + mapSort + " remains uninterpreted; no read-over-write axioms emitted.");
        return Apply(store ? "map-store" : "map-select", resultType, arguments);
      }
      var indexSorts = map.Arguments.Select(Type).ToArray();
      var valueSort = Type(map.Result);
      Require(arguments.Length == indexSorts.Length + (store ? 2 : 1) && arguments[0].Type == mapSort &&
          resultType == (store ? mapSort : valueSort) &&
          indexSorts.Select((sort, i) => arguments[i + 1].Type == sort).All(b => b) &&
          (!store || arguments[^1].Type == valueSort),
        "b3_map_signature", "Map operation differs from its resolved monomorphic signature", sourceApplication.tok);
      if (!mapHelperOrigins.ContainsKey(mapSort)) {
        mapHelperOrigins.Add(mapSort, "Monomorphic map helper origin: Boogie 73a0e214a87df85fc058270268c1d0706fd05bc9 " +
          "TypeErasureArguments.cs:298-464; map=" + mapSort + "; indices=(" + string.Join(",", indexSorts) +
          "); value=" + valueSort + "; encoding=direct-read-store-ITE; global-axioms=0");
      }
      if (store) {
        var application = (Ir.Application)Apply("map-store", resultType, arguments);
        mapStoreNames[mapSort] = application.Name;
        return application;
      }
      return ReadMap(mapSort, indexSorts, valueSort, arguments[0], arguments.Skip(1).ToArray(), sourceApplication.tok, 0);
    }

    private Ir.Expression ReadMap(string mapSort, string[] indexSorts, string valueSort,
      Ir.Expression map, Ir.Expression[] indexes, Bpl.IToken token, int depth) {
      Require(depth < Ir.Protocol.MaximumDepth, "b3_map_read_limit", "Nested map read lowering exceeds normalization bounds", token);
      var arity = indexSorts.Length;
      // Only this normalizer's typed store constructor can supply this provenance.
      // A read through a variable or ordinary source function is never replaced by its past assignment.
      if (map is not Ir.Application stored || !mapStoreNames.TryGetValue(mapSort, out var storeName) || stored.Name != storeName) {
        return Apply("map-select", valueSort, new[] { map }.Concat(indexes).ToArray());
      }
      Require(stored.Type == mapSort && stored.Arguments.Count == arity + 2 && stored.Arguments[0].Type == mapSort &&
          stored.Arguments[^1].Type == valueSort && indexes.Length == arity &&
          indexSorts.Select((sort, i) => stored.Arguments[i + 1].Type == sort && indexes[i].Type == sort).All(b => b),
        "b3_map_signature", "Owned store differs from the closed map signature", token);
      // Bound generated nodes before allocating conjunctions. The final traversal also counts copied occurrences.
      var estimate = 7L * arity + 8;
      Require(estimate <= Ir.Protocol.MaximumNodes - mapReadEstimatedNodes,
        "b3_map_read_limit", "Map read identities exceed normalization bounds", token);
      mapReadEstimatedNodes += (int)estimate;
      Ir.Expression sameIndex = new Ir.BooleanLiteral(true);
      for (var i = 0; i < arity; i++) {
        var equal = new Ir.Operation(Ir.Operator.Equal, "bool", new[] { stored.Arguments[i + 1], indexes[i] });
        sameIndex = new Ir.Operation(Ir.Operator.And, "bool", new[] { sameIndex, equal });
      }
      var unchanged = ReadMap(mapSort, indexSorts, valueSort, stored.Arguments[0], indexes, token, depth + 1);
      return new Ir.Operation(Ir.Operator.IfThenElse, valueSort, new[] { sameIndex, stored.Arguments[^1], unchanged });
    }

    private static Bpl.MapType ResolvedMapType(Bpl.Type type) {
      for (var depth = 0; depth < Ir.Protocol.MaximumDepth; depth++) {
        if (type is Bpl.TypeProxy proxy && ProxyTarget != null) { type = (Bpl.Type)ProxyTarget.GetValue(proxy); }
        else if (type is Bpl.TypeSynonymAnnotation alias) { type = alias.ExpandedType; }
        else { return type as Bpl.MapType; }
      }
      return null;
    }

    private bool TryMapObservationEquality(Bpl.QuantifierExpr quantifier, Environment env, bool old,
      int depth, out Ir.Expression observation) {
      observation = null;
      // Pinned type checking rewrites Bool equality to Iff; both express value equality here.
      // Metadata and all other quantifier shapes keep their existing translation.
      if (quantifier is not Bpl.ForallExpr || quantifier.Attributes != null || quantifier.Triggers != null ||
          quantifier.Body is not Bpl.NAryExpr { Fun: Bpl.BinaryOperator { Op: Bpl.BinaryOperator.Opcode.Eq or Bpl.BinaryOperator.Opcode.Iff } } equality ||
          equality.Args.Count != 2 ||
          equality.Args[0] is not Bpl.NAryExpr { Fun: Bpl.MapSelect } left ||
          equality.Args[1] is not Bpl.NAryExpr { Fun: Bpl.MapSelect } right ||
          left.Args.Count == 0 || right.Args.Count == 0) { return false; }
      var leftType = ResolvedMapType(left.Args[0].Type);
      var rightType = ResolvedMapType(right.Args[0].Type);
      if (leftType == null || rightType == null || leftType.TypeParameters.Count != 0 || rightType.TypeParameters.Count != 0 ||
          leftType.Arguments.Count != quantifier.Dummies.Count || rightType.Arguments.Count != quantifier.Dummies.Count ||
          left.Args.Count != quantifier.Dummies.Count + 1 || right.Args.Count != quantifier.Dummies.Count + 1) { return false; }
      // Each complete tuple uses every binder exactly once, in the declared order on both sides.
      for (var i = 0; i < quantifier.Dummies.Count; i++) {
        if (left.Args[i + 1] is not Bpl.IdentifierExpr first || first.Decl != quantifier.Dummies[i] ||
            right.Args[i + 1] is not Bpl.IdentifierExpr second || second.Decl != quantifier.Dummies[i] ||
            Type(quantifier.Dummies[i].TypedIdent.Type) != Type(leftType.Arguments[i]) ||
            Type(quantifier.Dummies[i].TypedIdent.Type) != Type(rightType.Arguments[i])) { return false; }
      }
      var mapSort = Type(leftType);
      if (mapSort != Type(rightType) || Type(left.Type) != Type(leftType.Result) || Type(right.Type) != Type(rightType.Result)) { return false; }
      var captured = new List<(Bpl.Variable Variable, bool Old)>();
      FreeVariables(left.Args[0], new HashSet<Bpl.Variable>(), captured, old);
      FreeVariables(right.Args[0], new HashSet<Bpl.Variable>(), captured, old);
      if (captured.Any(capture => quantifier.Dummies.Contains(capture.Variable))) { return false; }
      // Only the two map VALUES are captured, including their current/old environment substitutions.
      var firstMap = Expr(left.Args[0], env, old, depth + 1);
      var secondMap = Expr(right.Args[0], env, old, depth + 1);
      Require(firstMap.Type == mapSort && secondMap.Type == mapSort,
        "b3_map_signature", "Observation equality differs from its closed map signature", quantifier.tok);
      observation = Apply("map-observation-equality:" + mapSort, "bool", new[] { firstMap, secondMap });
      if (!mapObservationOrigins.ContainsKey(mapSort)) {
        mapObservationOrigins.Add(mapSort, "Map observation equality abstraction: map=" + mapSort +
          "; exact complete-tuple forall read equality is represented by an uninterpreted Bool predicate of the two map values; " +
          "no observation instances, extensionality or reverse map equality are asserted. Other quantifiers remain unchanged.");
      }
      return true;
    }

    private Ir.Expression Expr(Bpl.Expr expression, Environment env, bool old = false, int depth = 0) {
      Require(expression != null && depth < Ir.Protocol.MaximumDepth && ++expressionCount <= Ir.Protocol.MaximumNodes,
        "b3_expression_limit", "Missing expression or normalization resource bound exceeded", expression?.tok ?? unit.tok);
      var type = Type(expression.Type);
      switch (expression) {
        case Bpl.LiteralExpr literal when literal.Val is bool boolean: return new Ir.BooleanLiteral(boolean);
        case Bpl.LiteralExpr literal when literal.Val is BigNum integer:
          return new Ir.IntegerLiteral(CaptureInteger(integer.ToBigInteger, expression.tok));
        case Bpl.LiteralExpr literal when literal.Val is BigDec real:
          return CaptureReal(real, expression.tok);
        case Bpl.IdentifierExpr identifier: return Variable(identifier.Decl, env, old);
        case Bpl.OldExpr previous: return Expr(previous.Expr, env, true, depth + 1);
        case Bpl.NAryExpr application: {
          var args = application.Args.Select(a => Expr(a, env, old, depth + 1)).ToArray();
          switch (application.Fun) {
            case Bpl.UnaryOperator unary:
              return new Ir.Operation(unary.Op == Bpl.UnaryOperator.Opcode.Not ? Ir.Operator.Not : Ir.Operator.Negate, type, args);
            case Bpl.BinaryOperator binary: return Binary(binary.Op, type, args, expression.tok);
            case Bpl.IfThenElse: return new Ir.Operation(Ir.Operator.IfThenElse, type, args);
            case Bpl.ArithmeticCoercion coercion:
              return Coercion(coercion.Coercion, type, args, expression.tok);
            case Bpl.TypeCoercion:
              Require(args.Length == 1 && args[0].Type == type, "b3_coercion", "Nontrivial type coercion is unsupported", expression.tok);
              return args[0];
            case Bpl.MapSelect: return MapOperation(application, type, args, false);
            case Bpl.MapStore: return MapOperation(application, type, args, true);
            case Bpl.FunctionCall call:
              Require(call.Func != null, "b3_resolution", "Unresolved function call", expression.tok);
              if (TryNativeIntegerBody(call.Func, type, args, out var arithmetic) ||
                  TryNativeIntegerAxiom(call.Func, type, args, out arithmetic) ||
                  TryNativeRealConversion(call.Func, type, args, out arithmetic)) { return arithmetic; }
              Require(!HasPrimitiveArithmeticDefinition(call.Func), "b3_arithmetic",
                "Called primitive arithmetic definition is outside the reviewed substitution routes", expression.tok);
              var projection = IdentityProjection(call.Func);
              if (projection >= 0 && projection < args.Length && args[projection].Type == type) { return args[projection]; }
              Require(source.TopLevelDeclarations.Contains(call.Func), "b3_declaration_identity",
                "Opaque function is not an active declaration in the typed source artifact", expression.tok);
              var instantiation = call.Func.TypeParameters.Count == 0 ? "" :
                string.Join(",", call.Func.TypeParameters.Select(p => {
                  Require(application.TypeParameters != null, "b3_instantiation", "Missing resolved function instantiation", expression.tok);
                  return Type(application.TypeParameters[p]);
                }));
              var applied = (Ir.Application)Apply("function:" + call.Func.Name + "<" + instantiation + ">", type, args);
              functionOwners[applied.Name] = call.Func;
              return applied;
            default: throw new Unsupported("b3_operator", "Unsupported expression operator " + application.Fun.GetType().Name, expression.tok);
          }
        }
        case Bpl.QuantifierExpr quantifier: {
          Require(quantifier.TypeParameters.Count == 0 && quantifier.Dummies.Count > 0,
            "b3_quantifier", "Type-polymorphic or empty quantifiers require specialization", quantifier.tok);
          Require(quantifier.Dummies.All(v => v.TypedIdent.WhereExpr == null), "b3_bound_where", "Bound-variable where clauses are unsupported", quantifier.tok);
          if (TryMapObservationEquality(quantifier, env, old, depth, out var observation)) { return observation; }
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
    private string CaptureInteger(BigInteger value, Bpl.IToken token) {
      // Check storage before decimal conversion, then check exact decimal length.
      Require(value.GetBitLength() <= 4L * Ir.Protocol.MaximumIntegerCharacters,
        "b3_literal_limit", "Exact numeric literal exceeds its size bound", token);
      var digits = value.ToString(CultureInfo.InvariantCulture);
      Require(digits.Length <= Ir.Protocol.MaximumIntegerCharacters,
        "b3_literal_limit", "Exact numeric literal exceeds its size bound", token);
      ReserveNumeric(digits.Length, token);
      return digits;
    }

    private Ir.RationalLiteral CaptureReal(BigDec value, Bpl.IToken token) {
      var mantissa = value.Mantissa;
      if (mantissa.IsZero) {
        ReserveNumeric(2, token);
        return new Ir.RationalLiteral("0", "1");
      }
      Require(mantissa.GetBitLength() <= 4L * Ir.Protocol.MaximumIntegerCharacters,
        "b3_literal_limit", "Exact real literal exceeds its size bound", token);
      var digits = mantissa.ToString(CultureInfo.InvariantCulture);
      var exponent = (long)value.Exponent; // Widen before negation, including int.MinValue.
      var numeratorLength = digits.Length + Math.Max(0L, exponent);
      var denominatorLength = 1L + Math.Max(0L, -exponent);
      Require(numeratorLength <= Ir.Protocol.MaximumIntegerCharacters &&
        denominatorLength <= Ir.Protocol.MaximumIntegerCharacters,
        "b3_literal_limit", "Exact real literal expansion exceeds its size bound", token);
      ReserveNumeric(numeratorLength + denominatorLength, token);
      // Every allocation below is bounded by the exact decimal-length checks above.
      var numerator = exponent >= 0 ? mantissa * BigInteger.Pow(10, (int)exponent) : mantissa;
      var denominator = exponent < 0 ? BigInteger.Pow(10, (int)-exponent) : BigInteger.One;
      return new Ir.RationalLiteral(numerator.ToString(CultureInfo.InvariantCulture),
        denominator.ToString(CultureInfo.InvariantCulture));
    }

    private void ReserveNumeric(long characters, Bpl.IToken token) {
      numericCharacters += characters + 128; // Include the literal's JSON field overhead.
      Require(numericCharacters <= Ir.Protocol.MaximumMessageBytes, "b3_literal_limit",
        "Cumulative exact numeric literals exceed the message size bound", token);
    }

    private static Ir.Expression Coercion(Bpl.ArithmeticCoercion.CoercionType operation,
      string resultType, IReadOnlyList<Ir.Expression> args, Bpl.IToken token) {
      var toReal = operation == Bpl.ArithmeticCoercion.CoercionType.ToReal;
      Require((operation is Bpl.ArithmeticCoercion.CoercionType.ToReal or Bpl.ArithmeticCoercion.CoercionType.ToInt) &&
        args.Count == 1 && args[0].Type == (toReal ? "int" : "real") && resultType == (toReal ? "real" : "int"),
        "b3_arithmetic", "Native arithmetic coercion has an invalid signature", token);
      return new Ir.Operation(toReal ? Ir.Operator.ToReal : Ir.Operator.ToInt, resultType, args.ToArray());
    }

    private bool TryNativeRealConversion(Bpl.Function function, string resultType,
      IReadOnlyList<Ir.Expression> args, out Ir.Expression expression) {
      expression = null;
      if (args.Count != 1 || !TryCoercionDefinition(function, new HashSet<Bpl.Function>(), 0, out var operation)) {
        return false;
      }
      var toReal = operation == Bpl.ArithmeticCoercion.CoercionType.ToReal;
      if (args[0].Type != (toReal ? "int" : "real") || resultType != (toReal ? "real" : "int")) { return false; }
      expression = Coercion(operation, resultType, args, function.tok);
      return true;
    }

    private bool TryCoercionDefinition(Bpl.Function function, HashSet<Bpl.Function> visited, int depth,
      out Bpl.ArithmeticCoercion.CoercionType operation) {
      operation = default;
      if (function == null || depth >= Ir.Protocol.MaximumDepth || !visited.Add(function) ||
          function.TypeParameters.Count != 0 || function.InParams.Count != 1 || function.OutParams.Count != 1 ||
          function.InParams[0].TypedIdent.WhereExpr != null) { return false; }
      var formal = function.InParams[0];
      var result = function.OutParams[0].TypedIdent.Type;
      if (formal.TypedIdent.Type.IsInt && result.IsReal) { operation = Bpl.ArithmeticCoercion.CoercionType.ToReal; }
      else if (formal.TypedIdent.Type.IsReal && result.IsInt) { operation = Bpl.ArithmeticCoercion.CoercionType.ToInt; }
      else { return false; }

      if (function.Body != null) {
        if (IsDirectCoercion(function.Body, formal, operation)) { return true; }
        // The actual Floor Body composes a unary call with a separately justified conversion.
        if (function.Body is Bpl.NAryExpr { Fun: Bpl.FunctionCall call } body && body.Args.Count == 1 &&
            SamePrimitiveType(body.Type, result) && IsMonomorphic(body) &&
            body.Args[0] is Bpl.IdentifierExpr argument && ReferenceEquals(argument.Decl, formal) &&
            TryCoercionDefinition(call.Func, visited, depth + 1, out var nested) && nested == operation) { return true; }
        return false;
      }
      if (!function.AlwaysRevealed || !HasActiveDefinitionAxiom(function) ||
          function.DefinitionAxiom.Expr is not Bpl.ForallExpr forall || forall.TypeParameters.Count != 0 ||
          forall.Dummies.Count != 1 || forall.Dummies[0].TypedIdent.WhereExpr != null ||
          !SamePrimitiveType(forall.Dummies[0].TypedIdent.Type, formal.TypedIdent.Type) ||
          forall.Body is not Bpl.NAryExpr { Fun: Bpl.BinaryOperator { Op: Bpl.BinaryOperator.Opcode.Eq } } equality ||
          equality.Args.Count != 2) { return false; }
      var definingCall = equality.Args[0];
      if (definingCall is Bpl.NAryExpr { Fun: Bpl.TypeCoercion } coercion) {
        if (coercion.Args.Count != 1 || !SamePrimitiveType(coercion.Type, result) ||
            !SamePrimitiveType(coercion.Args[0].Type, result)) { return false; }
        definingCall = coercion.Args[0];
      }
      return definingCall is Bpl.NAryExpr { Fun: Bpl.FunctionCall defining } application &&
        ReferenceEquals(defining.Func, function) && SamePrimitiveType(application.Type, result) &&
        IsMonomorphic(application) && application.Args.Count == 1 &&
        application.Args[0] is Bpl.IdentifierExpr actual && ReferenceEquals(actual.Decl, forall.Dummies[0]) &&
        IsDirectCoercion(equality.Args[1], forall.Dummies[0], operation);
    }

    private static bool IsMonomorphic(Bpl.NAryExpr expression) => expression.TypeParameters == null ||
      expression.TypeParameters.FormalTypeParams.Count == 0;

    private static bool SamePrimitiveType(Bpl.Type first, Bpl.Type second) => first != null && second != null &&
      (first.IsInt && second.IsInt || first.IsReal && second.IsReal);

    private static bool IsDirectCoercion(Bpl.Expr expression, Bpl.Variable formal,
      Bpl.ArithmeticCoercion.CoercionType operation) =>
      expression is Bpl.NAryExpr { Fun: Bpl.ArithmeticCoercion coercion } body && coercion.Coercion == operation &&
      body.Args.Count == 1 && body.Args[0] is Bpl.IdentifierExpr identifier && ReferenceEquals(identifier.Decl, formal) &&
      (operation == Bpl.ArithmeticCoercion.CoercionType.ToReal
        ? formal.TypedIdent.Type.IsInt && body.Type?.IsReal == true
        : formal.TypedIdent.Type.IsReal && body.Type?.IsInt == true);

    private static bool TryNativeIntegerBody(Bpl.Function function, string resultType,
      IReadOnlyList<Ir.Expression> args, out Ir.Expression expression) {
      expression = null;
      if (resultType != "int" || args.Count != 2 || args.Any(arg => arg.Type != "int") ||
          function.TypeParameters.Count != 0 || function.InParams.Count != 2 || function.OutParams.Count != 1 ||
          function.InParams.Any(parameter => !parameter.TypedIdent.Type.IsInt) ||
          ReferenceEquals(function.InParams[0], function.InParams[1]) ||
          !function.OutParams[0].TypedIdent.Type.IsInt ||
          function.Body is not Bpl.NAryExpr { Fun: Bpl.BinaryOperator binary } body ||
          binary.Op is not (Bpl.BinaryOperator.Opcode.Div or Bpl.BinaryOperator.Opcode.Mod) ||
          body.Type?.IsInt != true || body.Args.Count != 2 ||
          body.Args[0] is not Bpl.IdentifierExpr left || body.Args[1] is not Bpl.IdentifierExpr right ||
          !ReferenceEquals(left.Decl, function.InParams[0]) || !ReferenceEquals(right.Decl, function.InParams[1])) {
        return false;
      }
      // The pinned native translator expands the actual typed Function.Body by formal substitution.
      // Names, inline attributes and detached definition metadata supply no premise for this rewrite.
      expression = new Ir.Operation(binary.Op == Bpl.BinaryOperator.Opcode.Div ? Ir.Operator.Divide : Ir.Operator.Modulo,
        "int", args);
      return true;
    }

    private bool HasActiveDefinitionAxiom(Bpl.Function function) =>
      function.DefinitionAxiom != null && source.TopLevelDeclarations.Contains(function.DefinitionAxiom);

    private bool TryNativeIntegerAxiom(Bpl.Function function, string resultType,
      IReadOnlyList<Ir.Expression> args, out Ir.Expression expression) {
      expression = null;
      if (resultType != "int" || args.Count != 2 || args.Any(arg => arg.Type != "int") ||
          !function.AlwaysRevealed || function.Body != null || !HasActiveDefinitionAxiom(function) ||
          function.TypeParameters.Count != 0 || function.InParams.Count != 2 || function.OutParams.Count != 1 ||
          function.InParams.Any(parameter => !parameter.TypedIdent.Type.IsInt) ||
          ReferenceEquals(function.InParams[0], function.InParams[1]) ||
          !function.OutParams[0].TypedIdent.Type.IsInt ||
          function.DefinitionAxiom.Expr is not Bpl.ForallExpr forall || forall.TypeParameters.Count != 0 ||
          forall.Dummies.Count != 2 || ReferenceEquals(forall.Dummies[0], forall.Dummies[1]) ||
          forall.Dummies.Any(binder => !binder.TypedIdent.Type.IsInt || binder.TypedIdent.WhereExpr != null) ||
          forall.Body is not Bpl.NAryExpr { Fun: Bpl.BinaryOperator { Op: Bpl.BinaryOperator.Opcode.Eq } } equality ||
          equality.Args.Count != 2) {
        return false;
      }
      var definingCall = equality.Args[0];
      // CreateDefinitionAxiom puts a typed identity coercion around the defining call.
      if (definingCall is Bpl.NAryExpr { Fun: Bpl.TypeCoercion } coercion) {
        if (coercion.Args.Count != 1 || coercion.Type?.IsInt != true || coercion.Args[0].Type?.IsInt != true) {
          return false;
        }
        definingCall = coercion.Args[0];
      }
      if (definingCall is not Bpl.NAryExpr { Fun: Bpl.FunctionCall call } application ||
          !ReferenceEquals(call.Func, function) || application.Type?.IsInt != true || application.Args.Count != 2 ||
          application.TypeParameters != null && application.TypeParameters.FormalTypeParams.Count != 0 ||
          application.Args[0] is not Bpl.IdentifierExpr left || application.Args[1] is not Bpl.IdentifierExpr right ||
          !ReferenceEquals(left.Decl, forall.Dummies[0]) || !ReferenceEquals(right.Decl, forall.Dummies[1]) ||
          equality.Args[1] is not Bpl.NAryExpr { Fun: Bpl.BinaryOperator binary } body ||
          binary.Op is not (Bpl.BinaryOperator.Opcode.Div or Bpl.BinaryOperator.Opcode.Mod) ||
          body.Type?.IsInt != true || body.Args.Count != 2 ||
          body.Args[0] is not Bpl.IdentifierExpr dividend || body.Args[1] is not Bpl.IdentifierExpr divisor ||
          !ReferenceEquals(dividend.Decl, left.Decl) || !ReferenceEquals(divisor.Decl, right.Decl)) {
        return false;
      }
      // This exact source axiom constrains every pair, including zero divisors. It is used for
      // substitution only; no axiom or nonzero guard is added to the normalized program.
      expression = new Ir.Operation(binary.Op == Bpl.BinaryOperator.Opcode.Div ? Ir.Operator.Divide : Ir.Operator.Modulo,
        "int", args);
      return true;
    }

    private static bool HasPrimitiveArithmeticDefinition(Bpl.Function function) {
      static bool Primitive(Bpl.Expr body) => body is Bpl.NAryExpr { Fun: Bpl.ArithmeticCoercion } ||
        body is Bpl.NAryExpr { Fun: Bpl.BinaryOperator binary } &&
        binary.Op is Bpl.BinaryOperator.Opcode.Div or Bpl.BinaryOperator.Opcode.Mod or
          Bpl.BinaryOperator.Opcode.RealDiv or Bpl.BinaryOperator.Opcode.Pow;
      if (Primitive(function.Body)) { return true; }
      var defining = function.DefinitionBody ?? function.DefinitionAxiom?.Expr;
      if (defining is Bpl.ForallExpr forall) { defining = forall.Body; }
      if (defining is not Bpl.NAryExpr { Fun: Bpl.BinaryOperator { Op: Bpl.BinaryOperator.Opcode.Eq } } equality) { return false; }
      for (var side = 0; side < 2; side++) {
        var call = equality.Args[side];
        if (call is Bpl.NAryExpr { Fun: Bpl.TypeCoercion } coercion) { call = coercion.Args[0]; }
        if (call is Bpl.NAryExpr { Fun: Bpl.FunctionCall functionCall } && functionCall.Func == function &&
            Primitive(equality.Args[1 - side])) { return true; }
      }
      return false;
    }

    private int IdentityProjection(Bpl.Function function) {
      // Visibility analysis cannot hide AlwaysRevealed definitions. Other definitions remain opaque.
      if (!function.AlwaysRevealed) { return -1; }
      if (function.Body is Bpl.IdentifierExpr identifier) { return function.InParams.IndexOf(identifier.Decl); }
      // Native checker setup asserts top-level axioms; detached definition metadata supplies no premise.
      if (!HasActiveDefinitionAxiom(function)) { return -1; }
      // A universally quantified direct defining equality justifies this rewrite; an :identity claim alone does not.
      var axiom = function.DefinitionAxiom.Expr;
      if (axiom is Bpl.ForallExpr forall && forall.Body is Bpl.NAryExpr equality &&
          equality.Fun is Bpl.BinaryOperator { Op: Bpl.BinaryOperator.Opcode.Eq }) {
        for (var side = 0; side < 2; side++) {
          var defined = equality.Args[side];
          if (defined is Bpl.NAryExpr { Fun: Bpl.TypeCoercion } coercion) { defined = coercion.Args[0]; }
          if (defined is Bpl.NAryExpr call && call.Fun is Bpl.FunctionCall fc && fc.Func == function &&
              InstantiatesAllQuantifiedTypes(function, forall, call) &&
              equality.Args[1 - side] is Bpl.IdentifierExpr projected &&
              call.Args.Count == forall.Dummies.Count && call.Args.Select((a, i) => a is Bpl.IdentifierExpr id && id.Decl == forall.Dummies[i]).All(b => b)) {
            return forall.Dummies.IndexOf(projected.Decl);
          }
        }
      }
      return -1;
    }
    private static bool InstantiatesAllQuantifiedTypes(Bpl.Function function, Bpl.ForallExpr forall, Bpl.NAryExpr call) {
      // A definition at one closed instance does not justify rewriting every instance of a generic function.
      if (function.TypeParameters.Count != forall.TypeParameters.Count) { return false; }
      if (function.TypeParameters.Count == 0) { return true; }
      var instantiation = call.TypeParameters;
      if (instantiation == null || instantiation.FormalTypeParams.Count != function.TypeParameters.Count ||
          function.TypeParameters.Any(p => !instantiation.FormalTypeParams.Contains(p))) { return false; }
      var quantified = new HashSet<Bpl.TypeVariable>(forall.TypeParameters);
      var used = new HashSet<Bpl.TypeVariable>();
      foreach (var parameter in function.TypeParameters) {
        var actual = instantiation[parameter];
        for (var depth = 0; depth < Ir.Protocol.MaximumDepth; depth++) {
          if (actual is Bpl.TypeProxy proxy && ProxyTarget != null) { actual = (Bpl.Type)ProxyTarget.GetValue(proxy); }
          else if (actual is Bpl.TypeSynonymAnnotation alias) { actual = alias.ExpandedType; }
          else { break; }
        }
        if (actual is not Bpl.TypeVariable bound || !quantified.Contains(bound) || !used.Add(bound)) { return false; }
      }
      return used.SetEquals(quantified);
    }

    private static void FreeVariables(Bpl.Expr expression, HashSet<Bpl.Variable> bound,
      List<(Bpl.Variable Variable, bool Old)> found, bool old) {
      var pending = new Stack<(Bpl.Expr Expression, HashSet<Bpl.Variable> Bound, bool Old, int Depth)>();
      var seen = new HashSet<(Bpl.Variable, bool)>();
      var count = 0;
      pending.Push((expression, bound, old, 0));
      while (pending.Count > 0) {
        var item = pending.Pop();
        Require(item.Depth < Ir.Protocol.MaximumDepth && ++count <= Ir.Protocol.MaximumNodes,
          "b3_lambda_capture_limit", "Lambda capture traversal exceeds normalization bounds", item.Expression.tok);
        void Push(Bpl.Expr child, HashSet<Bpl.Variable> scope, bool previous) =>
          pending.Push((child, scope, previous, item.Depth + 1));
        switch (item.Expression) {
          case Bpl.IdentifierExpr identifier:
            if (!item.Bound.Contains(identifier.Decl) && seen.Add((identifier.Decl, item.Old))) {
              found.Add((identifier.Decl, item.Old));
            }
            break;
          case Bpl.NAryExpr nary:
            for (var i = nary.Args.Count - 1; i >= 0; i--) { Push(nary.Args[i], item.Bound, item.Old); }
            break;
          case Bpl.OldExpr previous: Push(previous.Expr, item.Bound, true); break;
          case Bpl.BinderExpr binder:
            Push(binder.Body, new HashSet<Bpl.Variable>(item.Bound.Concat(binder.Dummies)), item.Old);
            break;
          case Bpl.LetExpr let:
            Push(let.Body, new HashSet<Bpl.Variable>(item.Bound.Concat(let.Dummies)), item.Old);
            for (var i = let.Rhss.Count - 1; i >= 0; i--) { Push(let.Rhss[i], item.Bound, item.Old); }
            break;
          case Bpl.LiteralExpr: break;
          default: throw new Unsupported("b3_lambda_capture", "Unsupported lambda capture expression " + item.Expression.GetType().Name, item.Expression.tok);
        }
      }
    }
    private static Ir.Expression Binary(Bpl.BinaryOperator.Opcode op, string type, Ir.Expression[] args, Bpl.IToken token) {
      if (op is Bpl.BinaryOperator.Opcode.Div or Bpl.BinaryOperator.Opcode.Mod) {
        Require(type == "int" && args.Length == 2 && args.All(arg => arg.Type == "int"),
          "b3_arithmetic", "Native division and modulo require two integer operands and an integer result", token);
      }
      if (op == Bpl.BinaryOperator.Opcode.RealDiv) {
        Require(type == "real" && args.Length == 2 && args.All(arg => arg.Type is "int" or "real"),
          "b3_arithmetic", "Native real division requires numeric operands and a real result", token);
        // Match the pinned native translation; this is not general mixed-sort promotion.
        return new Ir.Operation(Ir.Operator.RealDivide, "real", args.Select(arg => arg.Type == "int"
          ? new Ir.Operation(Ir.Operator.ToReal, "real", new[] { arg }) : arg).ToArray());
      }
      var kind = op switch {
        Bpl.BinaryOperator.Opcode.Add => Ir.Operator.Add, Bpl.BinaryOperator.Opcode.Sub => Ir.Operator.Subtract,
        Bpl.BinaryOperator.Opcode.Div => Ir.Operator.Divide, Bpl.BinaryOperator.Opcode.Mod => Ir.Operator.Modulo,
        Bpl.BinaryOperator.Opcode.Mul => Ir.Operator.Multiply, Bpl.BinaryOperator.Opcode.Eq => Ir.Operator.Equal,
        Bpl.BinaryOperator.Opcode.Neq => Ir.Operator.NotEqual, Bpl.BinaryOperator.Opcode.Lt or Bpl.BinaryOperator.Opcode.Gt => Ir.Operator.Less,
        Bpl.BinaryOperator.Opcode.Le or Bpl.BinaryOperator.Opcode.Ge => Ir.Operator.LessEqual,
        Bpl.BinaryOperator.Opcode.And => Ir.Operator.And, Bpl.BinaryOperator.Opcode.Or => Ir.Operator.Or,
        Bpl.BinaryOperator.Opcode.Imp => Ir.Operator.Implies, Bpl.BinaryOperator.Opcode.Iff => Ir.Operator.Equiv,
        _ => throw new Unsupported("b3_arithmetic", "Float division and power are unsupported pending correspondence", token)
      };
      if (op is Bpl.BinaryOperator.Opcode.Gt or Bpl.BinaryOperator.Opcode.Ge) { args = new[] { args[1], args[0] }; }
      return new Ir.Operation(kind, type, args);
    }
    private void Where(Bpl.Expr where, Environment env, List<Ir.Statement> statements) {
      if (where != null) { statements.Add(new Ir.Assume(Expr(where, env))); }
    }
    private Ir.Check Check(Bpl.Expr condition, Environment env, Bpl.IToken token, string description, Bpl.QKeyValue attributes, string role = "assert", Bpl.Absy sourceAnchor = null) {
      var expression = Expr(condition, env);
      var subsumption = Bpl.QKeyValue.FindIntAttribute(attributes, "subsumption", -1);
      var mode = subsumption switch { 0 => Bpl.CoreOptions.SubsumptionOption.Never,
        1 => Bpl.CoreOptions.SubsumptionOption.NotForQuantifiers, 2 => Bpl.CoreOptions.SubsumptionOption.Always,
        _ => options.UseSubsumption };
      var learn = mode == Bpl.CoreOptions.SubsumptionOption.Always ||
        mode == Bpl.CoreOptions.SubsumptionOption.NotForQuantifiers &&
        expression is not Ir.Quantifier && condition is not Bpl.QuantifierExpr;
      var id = "sO" + role + (obligations.Count + 1);
      var frame = visibility == null ? B3DefinitionVisibility.Frame.AllRevealed :
        sourceAnchor == null ? FallthroughMask() : visibility.Before(sourceAnchor);
      checkMasks.Add(id, (frame, expression, sourceAnchor));
      var origin = BoogieGenerator.ToDafnyToken(token);
      obligations.Add(new Ir.SourceIdentity(id, origin.Uri?.AbsoluteUri ?? token.filename ?? "",
        Math.Max(0, token.line), Math.Max(0, token.col), description));
      return new Ir.Check(id, expression, learn);
    }
    private Ir.Statement Exit(Environment env, Bpl.Absy sourceAnchor = null) {
      var statements = new List<Ir.Statement>();
      foreach (var ensures in unit.Proc.Ensures) {
        ValidateAttributes(ensures.Attributes, "ensures", ensures.tok);
        if (!ensures.Free) { statements.Add(Check(ensures.Condition, env, ensures.tok,
          ensures.Description?.FailureDescription ?? "postcondition", null, "post", sourceAnchor)); }
        else if (ensures.CanAlwaysAssume()) { statements.Add(new Ir.Assume(Expr(ensures.Condition, env))); }
      }
      statements.Add(new Ir.Return());
      return new Ir.Block(statements.ToArray());
    }
    private sealed class Control {
      public readonly Dictionary<string, string> Forward;
      public readonly Dictionary<Bpl.BigBlock, string> Break;
      public Control(Dictionary<string, string> forward = null, Dictionary<Bpl.BigBlock, string> breaks = null) {
        Forward = forward ?? new(StringComparer.Ordinal); Break = breaks ?? new();
      }
      public Control WithBreak(Bpl.BigBlock block, string label) {
        var breaks = new Dictionary<Bpl.BigBlock, string>(Break) { [block] = label };
        return new Control(Forward, breaks);
      }
    }

    // Resolve/Typecheck consumes Blocks, while this normalizer consumes StructuredStmts.
    // Assert inventory is an explicit API boundary, not a producer-coherence assumption.
    private void ValidateRawAssertionCoverage() {
      var anchors = checkMasks.Values.Select(check => check.Origin).OfType<Bpl.AssertCmd>().ToHashSet();
      var pending = new Stack<(Bpl.Cmd Command, int Depth)>();
      Require(unit.Blocks.Count is > 0 and <= 256, "b3_cfg_correspondence", "Raw CFG exceeds correspondence bounds", unit.tok);
      foreach (var block in unit.Blocks) { foreach (var command in block.Cmds) { pending.Push((command, 0)); } }
      var visited = 0;
      while (pending.Count > 0) {
        var (command, depth) = pending.Pop();
        Require(command != null && depth < Ir.Protocol.MaximumDepth && ++visited <= Ir.Protocol.MaximumNodes,
          "b3_cfg_correspondence", "Raw command inventory exceeds correspondence bounds", unit.tok);
        if (command is Bpl.AssertCmd assertion) {
          Require(anchors.Contains(assertion), "b3_cfg_correspondence",
            "Raw assertion has no original normalized assert or invariant anchor", assertion.tok);
        }
        if (command is Bpl.StateCmd state) {
          foreach (var nested in state.Cmds) { pending.Push((nested, depth + 1)); }
        }
      }
    }

    // Bounded inspection also finds the labels requiring lexical exit wrappers.
    private void InspectControl(Bpl.StmtList root) {
      var pending = new Stack<(object Node, int Depth)>();
      pending.Push((root, 0));
      while (pending.Count > 0) {
        var (node, depth) = pending.Pop();
        Require(depth < Ir.Protocol.MaximumDepth && ++statementCount <= Ir.Protocol.MaximumNodes,
          "b3_statement_limit", "Source control traversal exceeds normalization bounds", unit.tok);
        void Push(object child) { if (child != null) { pending.Push((child, depth + 1)); } }
        switch (node) {
          case Bpl.StmtList list:
            if (list.PrefixCommands != null) { foreach (var command in list.PrefixCommands) { Push(command); } }
            foreach (var block in list.BigBlocks) { Push(block); }
            break;
          case Bpl.BigBlock block:
            foreach (var command in block.simpleCmds) { Push(command); }
            Push(block.ec); Push(block.tc); break;
          case Bpl.IfCmd conditional:
            Push(conditional.Thn); Push(conditional.ElseIf); Push(conditional.ElseBlock); break;
          case Bpl.WhileCmd loop:
            Push(loop.Body); foreach (var invariant in loop.Invariants) { Push(invariant); } break;
          case Bpl.StateCmd state: foreach (var command in state.Cmds) { Push(command); } break;
          case Bpl.HideRevealCmd hide: visibilityCommands.Add(hide); break;
          case Bpl.ChangeScope: break;
          case Bpl.GotoCmd jump:
            ValidateAttributes(jump.Attributes, "goto", jump.tok);
            Require(jump.LabelNames != null && jump.LabelNames.Count == 1, "b3_transfer",
              "Only single-target lexical forward jumps are supported", jump.tok);
            jumpTargets.Add(jump.LabelNames[0]); break;
          case Bpl.ReturnCmd returned: explicitReturns.Add(returned); ValidateAttributes(returned.Attributes, "return", returned.tok); break;
        }
      }
      foreach (var target in jumpTargets.OrderBy(t => t, StringComparer.Ordinal)) { jumpLabels.Add(target, "sC" + ++controlNumber); }
    }

    private Ir.Statement Structured(Bpl.StmtList list, Environment env, Control control, int depth = 0) {
      Require(depth < Ir.Protocol.MaximumDepth, "b3_statement_limit", "Structured nesting exceeds normalization bound", list.EndCurly);
      var statements = new List<Ir.Statement>();
      var blocks = list.BigBlocks;
      var forward = new Dictionary<string, string>(control.Forward, StringComparer.Ordinal);
      foreach (var block in blocks) {
        if (block.LabelName != null && jumpTargets.Contains(block.LabelName)) { forward[block.LabelName] = jumpLabels[block.LabelName]; }
      }
      if (list.PrefixCommands != null) { statements.AddRange(list.PrefixCommands.Select(c => Command(c, env))); }
      foreach (var block in blocks) {
        if (block.LabelName != null && jumpTargets.Contains(block.LabelName)) {
          // This target is active only while processing the preceding prefix or its descendants.
          // Finishing or exiting that prefix resumes at this block.
          forward.Remove(block.LabelName);
          statements = new List<Ir.Statement> { new Ir.Labeled(jumpLabels[block.LabelName], new Ir.Block(statements.ToArray())) };
        }
        var nested = new Control(new Dictionary<string, string>(forward, StringComparer.Ordinal), control.Break);
        statements.AddRange(block.simpleCmds.Select(c => Command(c, env)));
        if (block.ec != null) { statements.Add(StructuredCommand(block.ec, env, nested, depth + 1, block)); }
        else if (block.tc is Bpl.ReturnCmd and not Bpl.ReturnExprCmd) { statements.Add(Exit(env, block.tc)); }
        else if (block.tc is Bpl.GotoCmd jump) {
          Require(jump.LabelNames.Count == 1 && nested.Forward.TryGetValue(jump.LabelNames[0], out _),
            "b3_transfer", "Backward, cross-region or multiple-target jumps are unsupported", jump.tok);
          statements.Add(new Ir.Exit(nested.Forward[jump.LabelNames[0]]));
        } else if (block.tc != null) { throw new Unsupported("b3_transfer", "Return-expression control is unsupported", block.tc.tok); }
      }
      return new Ir.Block(statements.ToArray());
    }

    private Ir.Statement StructuredCommand(Bpl.StructuredCmd command, Environment env, Control control, int depth,
      Bpl.BigBlock enclosing = null) {
      Require(depth < Ir.Protocol.MaximumDepth, "b3_statement_limit", "Structured nesting exceeds normalization bound", command.tok);
      if (command is Bpl.BreakCmd broken) {
        Require(broken.BreakEnclosure != null && control.Break.TryGetValue(broken.BreakEnclosure, out _),
          "b3_break", "Break does not name an active lexical enclosure", broken.tok);
        return new Ir.Exit(control.Break[broken.BreakEnclosure]);
      }
      if (command is Bpl.IfCmd conditional) {
        ValidateAttributes(conditional.Attributes, "conditional", conditional.tok);
        string exit = null;
        if (enclosing != null) {
          exit = "sC" + ++controlNumber;
          control = control.WithBreak(enclosing, exit);
        }
        var then = Structured(conditional.Thn, env, control, depth);
        var other = conditional.ElseIf != null ? StructuredCommand(conditional.ElseIf, env, control, depth + 1) :
          conditional.ElseBlock != null ? Structured(conditional.ElseBlock, env, control, depth) : new Ir.Block(Array.Empty<Ir.Statement>());
        Ir.Statement result = conditional.Guard == null ? new Ir.Choice(new[] { then, other }) :
          new Ir.Conditional(Expr(conditional.Guard, env), then, other);
        return exit == null ? result : new Ir.Labeled(exit, result);
      }
      if (command is Bpl.WhileCmd loop && enclosing != null) { return While(loop, enclosing, env, control, depth); }
      throw new Unsupported("b3_structured_control", "Unsupported structured control " + command.GetType().Name, command.tok);
    }

    private Ir.Statement While(Bpl.WhileCmd loop, Bpl.BigBlock enclosing, Environment env, Control control, int depth) {
      Require(loop.Yields.Count == 0 && options.KInductionDepth == -1 && options.LoopUnrollCount == -1 && !options.ConcurrentHoudini,
        "b3_loop_mode", "Yield invariants, k-induction, unrolling and concurrent Houdini need separate correspondence", loop.tok);
      var exit = "sC" + ++controlNumber;
      var initialization = LoopChecks(loop, env, "init");
      var iteration = new List<Ir.Statement>();
      if (loop.Guard != null) { iteration.Add(new Ir.Assume(Expr(loop.Guard, env))); }
      iteration.Add(Structured(loop.Body, env, control.WithBreak(enclosing, exit), depth));
      iteration.AddRange(LoopChecks(loop, env, "maint"));
      iteration.Add(new Ir.Assume(new Ir.BooleanLiteral(false)));
      var done = new List<Ir.Statement>();
      if (loop.Guard != null) { done.Add(new Ir.Assume(new Ir.Operation(Ir.Operator.Not, "bool", new[] { Expr(loop.Guard, env) }))); }
      done.Add(new Ir.Exit(exit));
      var choice = new Ir.Choice(new Ir.Statement[] { new Ir.Block(done.ToArray()), new Ir.Block(iteration.ToArray()) });
      var changed = NaturalLoopAssignments(enclosing, loop);
      var targetInfo = AssignmentTargets(choice);
      var targets = targetInfo.Continuations.Contains("normal") ? targetInfo.Targets : new HashSet<string>();
      Require(changed.All(v => targets.Contains(Name(v).Name)), "b3_loop_targets",
        "Native loop assignment targets do not cover the pinned natural-loop havoc", loop.tok);
      var header = new List<Ir.Statement>();
      // The native loop may havoc additional break-only targets. Their where clauses must NOT be assumed.
      foreach (var variable in changed) {
        if (outputWhere.TryGetValue(variable, out var where)) { Where(where.Expression, where.Environment, header); }
        else { Where(variable.TypedIdent.WhereExpr, env, header); }
      }
      foreach (var invariant in loop.Invariants) {
        ValidateAttributes(invariant.Attributes, "invariant", invariant.tok);
        header.Add(new Ir.Assume(Expr(invariant.Expr, env)));
      }
      header.Add(choice);
      initialization.Add(new Ir.Labeled(exit, new Ir.Loop(Array.Empty<Ir.Expression>(), new Ir.Block(header.ToArray()))));
      return new Ir.Block(initialization.ToArray());
    }

    private List<Ir.Statement> LoopChecks(Bpl.WhileCmd loop, Environment env, string role) {
      var checks = new List<Ir.Statement>();
      foreach (var invariant in loop.Invariants) {
        ValidateAttributes(invariant.Attributes, "invariant", invariant.tok);
        if (invariant is Bpl.AssertCmd assertion) {
          checks.Add(Check(assertion.Expr, env, assertion.tok,
            role == "init" ? "loop invariant initialization" : "loop invariant preservation", assertion.Attributes, role, assertion));
        } else if (invariant is Bpl.AssumeCmd && options.AlwaysAssumeFreeLoopInvariants) {
          checks.Add(new Ir.Assume(Expr(invariant.Expr, env)));
        } else { Require(invariant is Bpl.AssumeCmd, "b3_invariant", "Unknown invariant predicate", invariant.tok); }
      }
      return checks;
    }

    // Recompute graph information in owned sets. Never call ConvertToReducible, ComputePredecessors,
    // GetDesugaring or any VC transform. Irreducible or oversized CFGs fail closed.
    private IReadOnlyList<Bpl.Variable> NaturalLoopAssignments(Bpl.BigBlock enclosing, Bpl.WhileCmd loop) {
      Require(unit.Blocks.Count is > 0 and <= 256, "b3_cfg_limit", "Loop CFG exceeds audited graph bound", loop.tok);
      var blocks = new HashSet<Bpl.Block>(unit.Blocks);
      var successors = new Dictionary<Bpl.Block, List<Bpl.Block>>();
      var predecessors = unit.Blocks.ToDictionary(b => b, _ => new List<Bpl.Block>());
      foreach (var block in unit.Blocks) {
        var next = block.TransferCmd is Bpl.GotoCmd jump ? jump.LabelTargets : new List<Bpl.Block>();
        Require(next != null && next.All(blocks.Contains), "b3_cfg", "Unresolved or external CFG successor", block.tok);
        successors.Add(block, next);
        foreach (var target in next) { predecessors[target].Add(block); }
      }
      var entryBlock = unit.Blocks.SingleOrDefault(b => b.Label == enclosing.LabelName);
      Require(entryBlock?.TransferCmd is Bpl.GotoCmd { LabelTargets.Count: 1 }, "b3_loop_header", "Cannot identify structured loop entry", loop.tok);
      var header = ((Bpl.GotoCmd)entryBlock.TransferCmd).LabelTargets[0];
      Require(header.Cmds.SequenceEqual(loop.Invariants) && header.TransferCmd is Bpl.GotoCmd { LabelTargets.Count: 2 },
        "b3_loop_header", "Typed CFG does not match the pinned structured loop header", loop.tok);
      var reachable = new HashSet<Bpl.Block>();
      var pending = new Stack<Bpl.Block>(); pending.Push(unit.Blocks[0]);
      while (pending.Count > 0) {
        var block = pending.Pop(); if (!reachable.Add(block)) { continue; }
        foreach (var next in successors[block]) { pending.Push(next); }
      }
      if (!reachable.Contains(header)) { return Array.Empty<Bpl.Variable>(); }
      var dominators = reachable.ToDictionary(b => b, b => b == unit.Blocks[0] ? new HashSet<Bpl.Block> { b } : new HashSet<Bpl.Block>(reachable));
      bool changed;
      do {
        changed = false;
        foreach (var block in unit.Blocks.Where(b => reachable.Contains(b) && b != unit.Blocks[0])) {
          var incoming = predecessors[block].Where(reachable.Contains).ToArray();
          var current = new HashSet<Bpl.Block>(dominators[incoming[0]]);
          foreach (var previous in incoming.Skip(1)) { current.IntersectWith(dominators[previous]); }
          current.Add(block);
          if (!current.SetEquals(dominators[block])) { dominators[block] = current; changed = true; }
        }
      } while (changed);
      var region = new HashSet<Bpl.Block>();
      foreach (var backedge in predecessors[header].Where(b => reachable.Contains(b) && dominators[b].Contains(header))) {
        region.Add(header); pending.Push(backedge);
        while (pending.Count > 0) {
          var block = pending.Pop(); if (!region.Add(block)) { continue; }
          Require(dominators[block].Contains(header), "b3_irreducible_loop", "Loop has an unaudited external entry", loop.tok);
          foreach (var previous in predecessors[block].Where(reachable.Contains)) { pending.Push(previous); }
        }
      }
      var assigned = new HashSet<Bpl.Variable>();
      foreach (var block in unit.Blocks.Where(region.Contains)) {
        foreach (var command in block.Cmds) { assigned.UnionWith(Assigned(command)); }
      }
      return assigned.OrderBy(v => Name(v).Name, StringComparer.Ordinal).ToArray();
    }

    private IEnumerable<Bpl.Variable> Assigned(Bpl.Cmd command, int depth = 0) {
      Require(depth < Ir.Protocol.MaximumDepth, "b3_statement_limit", "Assigned-variable traversal exceeds normalization bound", command.tok);
      return command switch {
        Bpl.AssignCmd assignment => assignment.Lhss.Select(lhs => lhs.DeepAssignedVariable),
        Bpl.HavocCmd havoc => havoc.Vars.Select(v => v.Decl),
        Bpl.CallCmd call => call.Outs.Where(v => v != null).Select(v => v.Decl).Concat(call.Proc.Modifies.Select(v => v.Decl)),
        Bpl.StateCmd state => state.Cmds.SelectMany(c => Assigned(c, depth + 1)).Except(state.Locals),
        Bpl.PredicateCmd or Bpl.CommentCmd or Bpl.ChangeScope or Bpl.HideRevealCmd => Array.Empty<Bpl.Variable>(),
        _ => throw new Unsupported("b3_assigned_variables", "Unsupported natural-loop assignment command " + command.GetType().Name, command.tok)
      };
    }

    private sealed record TargetInfo(HashSet<string> TargetsSet, HashSet<string> Continuations) {
      public HashSet<string> Targets => TargetsSet;
    }
    // Mirrors the selected B3 AssignmentTargets algorithm, including abrupt control.
    private static TargetInfo AssignmentTargets(Ir.Statement root, int depth = 0) {
      Require(depth < Ir.Protocol.MaximumDepth, "b3_statement_limit", "Assignment-target traversal exceeds normalization bound", Bpl.Token.NoToken);
      const string normal = "normal";
      var targets = new HashSet<string>(); var points = new HashSet<string> { normal };
      void Merge(TargetInfo child) { targets.UnionWith(child.Targets); points.UnionWith(child.Continuations); }
      switch (root) {
        case Ir.Assign assign: targets.Add(assign.Variable); break;
        case Ir.Havoc havoc: targets.UnionWith(havoc.Variables); break;
        case Ir.Exit exit: points = new HashSet<string> { exit.Label }; break;
        case Ir.Return: points = new HashSet<string> { "return" }; break;
        case Ir.Block block:
          foreach (var statement in block.Statements) {
            if (!points.Remove(normal)) { break; }
            Merge(AssignmentTargets(statement, depth + 1));
          }
          break;
        case Ir.Choice choice:
          points.Clear(); foreach (var branch in choice.Branches) { Merge(AssignmentTargets(branch, depth + 1)); } break;
        case Ir.Conditional conditional:
          points.Clear(); Merge(AssignmentTargets(conditional.Then, depth + 1)); Merge(AssignmentTargets(conditional.Else, depth + 1)); break;
        case Ir.Loop loop:
          points.Clear(); Merge(AssignmentTargets(loop.Body, depth + 1)); points.Remove(normal); break;
        case Ir.Labeled labeled:
          points.Clear(); Merge(AssignmentTargets(labeled.Body, depth + 1));
          if (points.Remove(labeled.Name)) { points.Add(normal); } break;
      }
      return new TargetInfo(targets, points);
    }

    private void CheckOwnedBounds(Ir.Program program) {
      var pending = new Stack<(object Node, int Depth)>(); pending.Push((program.Unit.Body, 0));
      foreach (var axiom in program.Axioms) { pending.Push((axiom.Condition, 0)); }
      var count = program.Types.Count + program.Functions.Count + program.Axioms.Count + program.Unit.Variables.Count + obligations.Count;
      while (pending.Count > 0) {
        var (node, depth) = pending.Pop();
        Require(depth <= Ir.Protocol.MaximumDepth && ++count <= Ir.Protocol.MaximumNodes,
          "b3_owned_ir_limit", "Normalized IR exceeds protocol resource bounds", unit.tok);
        void Push(object child) => pending.Push((child, depth + 1));
        switch (node) {
          case Ir.Block block: foreach (var child in block.Statements) { Push(child); } break;
          case Ir.Assign assign: Push(assign.Value); break;
          case Ir.Check check: Push(check.Condition); break;
          case Ir.Assume assume: Push(assume.Condition); break;
          case Ir.Choice choice: foreach (var branch in choice.Branches) { Push(branch); } break;
          case Ir.Conditional conditional: Push(conditional.Condition); Push(conditional.Then); Push(conditional.Else); break;
          case Ir.Loop loop: Push(loop.Body); break;
          case Ir.Labeled labeled: Push(labeled.Body); break;
          case Ir.Application application: foreach (var argument in application.Arguments) { Push(argument); } break;
          case Ir.Operation operation: foreach (var argument in operation.Arguments) { Push(argument); } break;
          case Ir.Quantifier quantifier:
            Push(quantifier.Body); foreach (var pattern in quantifier.Patterns) { foreach (var term in pattern) { Push(term); } } break;
          case Ir.Let let: Push(let.Value); Push(let.Body); break;
          case Ir.Label label: Push(label.Body); break;
        }
      }
    }
    private Ir.Statement Command(Bpl.Cmd command, Environment env) {
      if (command is Bpl.ICarriesAttributes attributed) { ValidateAttributes(attributed.Attributes, "command", command.tok); }
      switch (command) {
        case Bpl.CommentCmd: return new Ir.Block(Array.Empty<Ir.Statement>());
        case Bpl.AssertCmd assertion: return Check(assertion.Expr, env, assertion.tok,
          assertion.Description?.FailureDescription ?? "assertion", assertion.Attributes, "assert", assertion);
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
          // Pinned StateCmd passification appends raw where predicates, without incarnation substitution.
          // Evaluating them in the current environment can strengthen the context after an outer variable changes.
          // Omit this entry-only context; explicit havocs still retain their normal where assumptions.
          statements.AddRange(state.Cmds.Select(c => Command(c, env)));
          return new Ir.Block(statements.ToArray());
        }
        case Bpl.CallCmd call: return Call(call, env);
        case Bpl.ChangeScope: return new Ir.Block(Array.Empty<Ir.Statement>());
        case Bpl.HideRevealCmd: return new Ir.Block(Array.Empty<Ir.Statement>());
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
      // The pinned call's StateCmd entry appends output where predicates without incarnation substitution.
      // Conservatively omit that entry-only context, and preserve the post-havoc where schedule below.
      var callEntry = new List<Ir.Statement>();
      var temporaryNames = substitutions.Values.Cast<Ir.Variable>().Select(v => v.Name).ToArray();
      if (temporaryNames.Length > 0) { callEntry.Add(new Ir.Havoc(temporaryNames)); }
      statements.InsertRange(0, callEntry);
      foreach (var requires in call.Proc.Requires) {
        ValidateAttributes(requires.Attributes, "callee requires", requires.tok);
        if (!requires.Free && !call.IsFree) { statements.Add(Check(requires.Condition, requirementAndWhere, call.tok,
          requires.Description?.FailureDescription ?? "call precondition", call.Attributes, "call", call)); }
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
