using System.Collections.Generic;
using System.Linq;
using DafnyCore.Verifier;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny;

public partial class BoogieGenerator {
  // This package constructs expressions only. Preparation permissions remain the
  // responsibility of the existing WF/declared-contract/guarded introduction path.
  internal enum ObligationPreparation { CheckedExpression, DeclaredContract, GuardedIntroduction }

  internal record PropositionInputs(BodyTranslationContext Body, ExpressionTranslator Translator,
    bool ApplyInduction, int InliningHeight, ObligationPreparation Preparation, Bpl.Expr Guard);

  internal record PropositionLowering(Expression Source, PropositionInputs Inputs,
    IReadOnlyList<SplitExprInfo> Pieces, Bpl.Expr Summary, bool SplitHappened);

  private PropositionLowering LowerProposition(BodyTranslationContext context, Expression condition,
    ExpressionTranslator etran, bool applyInduction = true, int heightLimit = int.MaxValue,
    ObligationPreparation preparation = ObligationPreparation.DeclaredContract, Bpl.Expr guard = null) {
    var explicitAssertion = preparation == ObligationPreparation.CheckedExpression;
    var savedStatement = stmtContext;
    var savedAdjustment = adjustFuelForExists;
    // Extra implicit checks start with the existing assertion policy. The
    // original checks and explicit assertions keep their original translator,
    // statement context, fuel choices and splitting decisions.
    var checking = explicitAssertion ? etran : etran.CloneForObligation();
    try {
      if (!explicitAssertion) {
        stmtContext = StmtType.ASSERT;
        adjustFuelForExists = true;
      }
      var pieces = new List<SplitExprInfo>();
      var split = TrSplitExpr(context, condition, pieces, true, heightLimit, applyInduction, checking);
      if (!split) {
        // TrAssertCondition has always translated the unsplit condition again.
        // Keep that exact formula, rather than replacing it by the splitter's
        // tentative result (which may have consumed a fuel adjustment).
        pieces.Clear();
        pieces.Add(ToSplitExprInfo(SplitExprInfo.K.Both, checking.TrExpr(condition)));
      }
      // Explicit assertions retain their existing statement publication path.
      // Construct a comparison summary there only for the structural observer.
      var summary = !explicitAssertion || flags.ObligationLowered != null
        ? AssertionSummary(condition, pieces, split, checking) : null;
      var lowering = new PropositionLowering(condition,
        new PropositionInputs(context, etran, applyInduction, heightLimit, preparation, guard), pieces, summary, split);
      flags.ObligationLowered?.Invoke(lowering);
      return lowering;
    } finally {
      if (!explicitAssertion) {
        stmtContext = savedStatement;
        adjustFuelForExists = savedAdjustment;
      }
    }
  }

  private Bpl.Expr AssertionSummary(Expression condition, IReadOnlyList<SplitExprInfo> pieces,
    bool split, ExpressionTranslator etran) {
    if (!split) { return pieces[0].E; }
    var savedStatement = stmtContext;
    var savedAdjustment = adjustFuelForExists;
    try {
      stmtContext = StmtType.ASSUME;
      adjustFuelForExists = true;
      return etran.CloneForObligation().TrExpr(condition);
    } finally {
      stmtContext = savedStatement;
      adjustFuelForExists = savedAdjustment;
    }
  }

  private void CheckPropositionUnderGuard(IOrigin origin, Expression condition, Bpl.Expr guard,
    ProofObligationDescription description, BoogieStmtListBuilder builder, ExpressionTranslator etran) {
    // A guarded introduction does not grant its can-call premise unconditionally.
    var lowering = LowerProposition(builder.Context, condition, etran,
      preparation: ObligationPreparation.GuardedIntroduction, guard: guard);
    foreach (var piece in lowering.Pieces) {
      if (piece.IsChecked) {
        builder.Add(AssertAndForget(builder.Context, ObligationOrigin(origin, piece.Tok), BplImp(guard, piece.E), description));
      }
    }
    var summary = TrAssumeCmd(origin, BplImp(guard, lowering.Summary));
    proofDependencies?.AddProofDependencyId(summary, origin,
      new AssumptionDependency(false, "checked guarded obligation", condition));
    builder.Add(summary);
  }

  private static IOrigin ObligationOrigin(IOrigin source, IOrigin piece) {
    // Synthesized built-in constraints can have NoToken origins. Related
    // locations must identify real source text; the obligation itself retains
    // the original boundary's location and message.
    static bool HasSourceLocations(IOrigin origin) {
      if (origin.Uri == null || origin.line <= 0) { return false; }
      return origin switch {
        NestedOrigin nested => HasSourceLocations(nested.Outer) && HasSourceLocations(nested.Inner),
        OriginWrapper wrapper => HasSourceLocations(wrapper.WrappedOrigin),
        _ => true
      };
    }
    return HasSourceLocations(piece) ? new NestedOrigin(source, piece) : source;
  }

  private record MethodPostconditionClause(AttributedExpression Source, IReadOnlyList<Bpl.Ensures> OriginalChecks);

  private readonly Dictionary<MethodOrConstructor, IReadOnlyList<MethodPostconditionClause>> methodPostconditionClauses = new();

  // Clone unresolved named identifiers, and rebind known formals and every
  // copied binder before traversing its body, attributes and trigger patterns.
  // Boogie's generic duplicator deliberately leaves identifier Decl references
  // alone, so cloning a binder without this map would change its binding.
  private sealed class MethodPostconditionDuplicator(Dictionary<Bpl.Variable, Bpl.Expr> replacements,
    Dictionary<string, Bpl.Expr> namedReplacements = null) : Bpl.Duplicator {
    public override Bpl.Expr VisitIdentifierExpr(Bpl.IdentifierExpr node) {
      if (node.Decl != null && replacements.TryGetValue(node.Decl, out var replacement)) { return replacement; }
      return node.Decl == null && namedReplacements != null && namedReplacements.TryGetValue(node.Name, out replacement)
        ? replacement : base.VisitIdentifierExpr(node);
    }

    private Bpl.Expr WithCopiedBindings(List<Bpl.Variable> variables,
      System.Func<List<Bpl.Variable>, Bpl.Expr> copyBody) {
      var copies = variables.Select(variable => {
        var copy = (Bpl.Variable)variable.Clone();
        copy.TypedIdent = (Bpl.TypedIdent)variable.TypedIdent.Clone();
        return copy;
      }).ToList();
      var saved = variables.Select(variable => replacements.GetValueOrDefault(variable)).ToList();
      var savedNames = namedReplacements == null ? null : variables.Select(variable =>
        namedReplacements.GetValueOrDefault(variable.TypedIdent.Name)).ToList();
      foreach (var pair in variables.Zip(copies)) {
        var identifier = new Bpl.IdentifierExpr(pair.Second.tok, pair.Second);
        replacements[pair.First] = identifier;
        if (namedReplacements != null) { namedReplacements[pair.First.TypedIdent.Name] = identifier; }
      }
      try {
        foreach (var pair in variables.Zip(copies)) {
          pair.Second.TypedIdent.Type = (Bpl.Type)Visit(pair.First.TypedIdent.Type);
          pair.Second.TypedIdent.WhereExpr = pair.First.TypedIdent.WhereExpr == null ? null : VisitExpr(pair.First.TypedIdent.WhereExpr);
          pair.Second.Attributes = pair.First.Attributes == null ? null : VisitQKeyValue(pair.First.Attributes);
        }
        return copyBody(copies);
      } finally {
        for (var i = 0; i < variables.Count; i++) {
          if (saved[i] == null) { replacements.Remove(variables[i]); }
          else { replacements[variables[i]] = saved[i]; }
          if (namedReplacements != null) {
            if (savedNames[i] == null) { namedReplacements.Remove(variables[i].TypedIdent.Name); }
            else { namedReplacements[variables[i].TypedIdent.Name] = savedNames[i]; }
          }
        }
      }
    }

    public override Bpl.Expr VisitBinderExpr(Bpl.BinderExpr node) =>
      WithCopiedBindings(node.Dummies, copies => {
        var result = (Bpl.BinderExpr)node.Clone();
        result.Dummies = copies;
        result.Body = VisitExpr(node.Body);
        result.Attributes = node.Attributes == null ? null : VisitQKeyValue(node.Attributes);
        if (node is Bpl.QuantifierExpr quantifier) {
          ((Bpl.QuantifierExpr)result).Triggers = quantifier.Triggers == null ? null : VisitTrigger(quantifier.Triggers);
        }
        return result;
      });

    public override Bpl.QuantifierExpr VisitQuantifierExpr(Bpl.QuantifierExpr node) =>
      (Bpl.QuantifierExpr)VisitBinderExpr(node);

    public override Bpl.Expr VisitLetExpr(Bpl.LetExpr node) {
      // The right-hand sides are outside the let bindings' scope.
      var rhss = node.Rhss.Select(VisitExpr).ToList();
      return WithCopiedBindings(node.Dummies, copies => {
        var result = (Bpl.LetExpr)node.Clone();
        result.Dummies = copies;
        result.Rhss = rhss;
        result.Body = VisitExpr(node.Body);
        result.Attributes = node.Attributes == null ? null : VisitQKeyValue(node.Attributes);
        return result;
      });
    }
  }

  private Bpl.PredicateCmd AssertMethodContract(IOrigin origin, Bpl.Expr condition,
    ProofObligationDescription description, BodyTranslationContext context) =>
    Assert(new ForceCheckOrigin(origin), condition, description,
      context with { AssertMode = AssertMode.Check });

  private sealed class ContractFuelLayerDescription : ProofObligationDescription {
    public override string SuccessDescription => "existing fuel layers agree";
    public override string FailureDescription => "could not establish equivalence between existing fuel layers";
    public override string ShortDescription => "fuel layer agreement";
  }

  private void CheckContractFuelLayers(IOrigin origin, BoogieStmtListBuilder checking,
    List<(Bpl.Expr Condition, Expression Source)> facts, Expression source, IEnumerable<Bpl.Expr> expressions) {
    // Instantiate the existing unconditional layer-synonym law, then check the
    // instances before using them. No can-call permission or body visibility is
    // inferred, and no existing check or fuel argument is replaced.
    var fuelFunctions = declarationMapping.Where(pair => pair.Key is Function f && f.IsFuelAware())
      .ToDictionary(pair => pair.Value.Name, pair => (
        Position: Enumerable.Range(0, pair.Value.InParams.Count).FirstOrDefault(index =>
          pair.Value.InParams[index].TypedIdent.Name == "$ly" &&
          pair.Value.InParams[index].TypedIdent.Type == Predef.LayerType, -1), Arity: pair.Value.InParams.Count));
    var equalities = new List<Bpl.Expr>();
    var visited = new HashSet<Bpl.Expr>(ReferenceEqualityComparer.Instance);
    void Gather(Bpl.Expr expression) {
      // Do not extract applications from binders, lets or old-expression scopes.
      // Unsupported terms retain all existing checking and publication paths.
      if (expression is not Bpl.NAryExpr application || !visited.Add(expression)) { return; }
      if (application.Fun is Bpl.FunctionCall function &&
          fuelFunctions.TryGetValue(function.FunctionName, out var signature) &&
          signature.Position >= 0 && application.Args.Count == signature.Arity &&
          application.Args[signature.Position] is Bpl.NAryExpr successor && successor.Args.Count == 1 &&
          successor.Fun is Bpl.FunctionCall { FunctionName: "$LS" }) {
        var predecessor = (Bpl.NAryExpr)application.Clone();
        predecessor.Args = application.Args.ToList();
        predecessor.Args[signature.Position] = successor.Args[0];
        equalities.Add(Bpl.Expr.Eq(application, predecessor));
      }
      foreach (var argument in application.Args) { Gather(argument); }
    }
    foreach (var expression in expressions) { Gather(expression); }
    if (equalities.Count == 0) { return; }
    var agreement = equalities.Aggregate((left, right) => BplAnd(left, right));
    checking.Add(AssertMethodContract(origin, agreement, new ContractFuelLayerDescription(), checking.Context));
    facts.Add((agreement, source));
  }

  // Translation has not yet resolved every Boogie identifier. Collect only
  // known declaration identities; the scoped copier resolves source binders
  // lexically while leaving unrelated named identifiers unchanged.
  private sealed class ContractReferencedVariableCollector : Bpl.Duplicator {
    public readonly HashSet<Bpl.Variable> Variables = new(ReferenceEqualityComparer.Instance);
    public override Bpl.Expr VisitIdentifierExpr(Bpl.IdentifierExpr node) {
      if (node.Decl != null) { Variables.Add(node.Decl); }
      return base.VisitIdentifierExpr(node);
    }
  }

  private sealed class ContractScopedFuelLayerDescription : ProofObligationDescription {
    public override string SuccessDescription => "existing fuel layers agree in their scope";
    public override string FailureDescription => "could not establish scoped equivalence between existing fuel layers";
    public override string ShortDescription => "scoped fuel layer agreement";
  }

  private void CheckContractQuantifiedFuelLayers(IOrigin origin, BoogieStmtListBuilder checking,
    List<(Bpl.Expr Condition, Expression Source)> facts, Expression source, IEnumerable<Bpl.Expr> expressions) {
    var equalities = ContractScopedFuelEqualities(origin, expressions, false);
    if (equalities.Count == 0) { return; }
    var conjunction = equalities.Aggregate((left, right) => BplAnd(left, right));
    checking.Add(AssertMethodContract(origin, conjunction, new ContractScopedFuelLayerDescription(), checking.Context));
    facts.Add((conjunction, source));
  }

  private List<Bpl.Expr> ContractScopedFuelEqualities(IOrigin origin,
    IEnumerable<Bpl.Expr> expressions, bool includeGround,
    IReadOnlyDictionary<string, (int Position, int Arity)> additionalFunctions = null) {
    // These are checked instances of the same unconditional layer-synonym law
    // used above. Close over every referenced source dummy universally, for
    // either source quantifier polarity. Never export an existential witness.
    var fuelFunctions = additionalFunctions ?? declarationMapping.Where(pair => pair.Key is Function f && f.IsFuelAware())
      .ToDictionary(pair => pair.Value.Name, pair => (
        Position: Enumerable.Range(0, pair.Value.InParams.Count).FirstOrDefault(index =>
          pair.Value.InParams[index].TypedIdent.Name == "$ly" &&
          pair.Value.InParams[index].TypedIdent.Type == Predef.LayerType, -1), Arity: pair.Value.InParams.Count));
    var scopes = new List<List<Bpl.Variable>>();
    var equalities = new List<Bpl.Expr>();
    void Gather(Bpl.Expr expression, Dictionary<Bpl.Variable, Bpl.Expr> substitutions,
      Dictionary<string, Bpl.Expr> namedSubstitutions) {
      if (expression is Bpl.QuantifierExpr quantifier) {
        // Polymorphic Boogie binders need a separate type-binding copier.
        // Retain their original checks without extracting applications.
        if (quantifier.TypeParameters.Count != 0) { return; }
        var inner = new Dictionary<Bpl.Variable, Bpl.Expr>(substitutions);
        var innerNames = new Dictionary<string, Bpl.Expr>(namedSubstitutions);
        foreach (var dummy in quantifier.Dummies) {
          inner.Remove(dummy);
          innerNames[dummy.TypedIdent.Name] = new Bpl.IdentifierExpr(dummy.tok, dummy);
        }
        scopes.Add(quantifier.Dummies);
        Gather(quantifier.Body, inner, innerNames);
        scopes.RemoveAt(scopes.Count - 1);
        return;
      }
      if (expression is Bpl.LetExpr let) {
        // Let right-hand sides are evaluated in the outer lexical scope.
        foreach (var rhs in let.Rhss) { Gather(rhs, substitutions, namedSubstitutions); }
        var inner = new Dictionary<Bpl.Variable, Bpl.Expr>(substitutions);
        var innerNames = new Dictionary<string, Bpl.Expr>(namedSubstitutions);
        var copier = new MethodPostconditionDuplicator(substitutions, namedSubstitutions);
        foreach (var pair in let.Dummies.Zip(let.Rhss)) {
          var rhs = copier.VisitExpr(pair.Second);
          inner[pair.First] = rhs;
          innerNames[pair.First.TypedIdent.Name] = rhs;
        }
        Gather(let.Body, inner, innerNames);
        return;
      }
      // Do not extract from lambdas or move an application out of old(...).
      if (expression is not Bpl.NAryExpr original) { return; }
      if ((includeGround || scopes.Count != 0) && original.Fun is Bpl.FunctionCall function &&
          fuelFunctions.TryGetValue(function.FunctionName, out var signature) &&
          signature.Position >= 0 && original.Args.Count == signature.Arity) {
        var application = (Bpl.NAryExpr)new MethodPostconditionDuplicator(substitutions, namedSubstitutions).VisitExpr(original);
        if (application.Args[signature.Position] is Bpl.NAryExpr successor && successor.Args.Count == 1 &&
            successor.Fun is Bpl.FunctionCall { FunctionName: "$LS" }) {
          var predecessor = (Bpl.NAryExpr)application.Clone();
          predecessor.Args = application.Args.ToList();
          predecessor.Args[signature.Position] = successor.Args[0];
          var equality = Bpl.Expr.Eq(application, predecessor);
          var referenced = new ContractReferencedVariableCollector();
          referenced.VisitExpr(equality);
          var needed = new List<Bpl.Variable>();
          var seen = new HashSet<Bpl.Variable>(ReferenceEqualityComparer.Instance);
          foreach (var variable in scopes.SelectMany(scope => scope)) {
            if (referenced.Variables.Contains(variable) && seen.Add(variable)) { needed.Add(variable); }
          }
          // Dependent or attributed dummies need a separate closure rule.
          // Skip only this additional instance; retain every original check.
          if (needed.Any(variable => variable.TypedIdent.WhereExpr != null || variable.Attributes != null)) { return; }
          Bpl.Expr agreement = equality;
          if (needed.Count != 0) {
            // Fresh names also keep distinct shadowed source declarations
            // distinct when their universal closure is flattened.
            var copies = needed.Select(variable => {
              var copy = (Bpl.Variable)variable.Clone();
              copy.TypedIdent = (Bpl.TypedIdent)variable.TypedIdent.Clone();
              copy.TypedIdent.Name = CurrentIdGenerator.FreshId("$contractFuel#");
              return copy;
            }).ToList();
            var replacements = needed.Zip(copies).ToDictionary(pair => pair.First,
              pair => (Bpl.Expr)new Bpl.IdentifierExpr(pair.Second.tok, pair.Second));
            var copier = new MethodPostconditionDuplicator(replacements);
            foreach (var pair in needed.Zip(copies)) {
              pair.Second.TypedIdent.Type = (Bpl.Type)copier.Visit(pair.First.TypedIdent.Type);
              pair.Second.TypedIdent.WhereExpr = pair.First.TypedIdent.WhereExpr == null
                ? null : copier.VisitExpr(pair.First.TypedIdent.WhereExpr);
              pair.Second.Attributes = pair.First.Attributes == null ? null : copier.VisitQKeyValue(pair.First.Attributes);
            }
            agreement = new Bpl.ForallExpr(origin, copies, BplTrigger(copier.VisitExpr(application)),
              copier.VisitExpr(equality));
          }
          equalities.Add(agreement);
        }
      }
      foreach (var argument in original.Args) { Gather(argument, substitutions, namedSubstitutions); }
    }
    foreach (var expression in expressions) {
      Gather(expression, new Dictionary<Bpl.Variable, Bpl.Expr>(), new Dictionary<string, Bpl.Expr>());
    }
    return equalities;
  }

  private IReadOnlyDictionary<string, (int Position, int Arity)> AdditionalContextFuelFunctions() {
    // The declaration cache contains only functions already emitted. Refinement
    // implementations can precede their inherited function declarations. Read
    // resolved source signatures without emitting declarations, assigning names
    // or changing the original cache. This mirrors GetFunctionBoogieDefinition.
    var existing = declarationMapping.Values.Select(function => function.Name).ToHashSet();
    var result = new Dictionary<string, (int Position, int Arity)>();
    foreach (var function in program.RawModules().SelectMany(module => module.TopLevelDecls)
               .OfType<TopLevelDeclWithMembers>().SelectMany(declaration => declaration.Members).OfType<Function>()) {
      if (!function.IsFuelAware() || existing.Contains(function.FullSanitizedName) ||
          function.IsOpaque || function.IsMadeImplicitlyOpaque(options)) { continue; }
      var position = GetTypeParams(function).Count();
      var arity = position + 1 + (function is TwoStateFunction ? 1 : 0) + (function.ReadsHeap ? 1 : 0) +
        (function.IsStatic ? 0 : 1) + function.Ins.Count;
      var signature = (Position: position, Arity: arity);
      if (result.TryGetValue(function.FullSanitizedName, out var previous) && previous != signature) {
        throw new System.InvalidOperationException("Conflicting context fuel signatures");
      }
      result[function.FullSanitizedName] = signature;
    }
    return result;
  }

  private sealed class ContractContextFuelLayerDescription : ProofObligationDescription {
    public override string SuccessDescription => "existing context fuel layers agree";
    public override string FailureDescription => "could not establish fuel equivalence for an existing context term";
    public override string ShortDescription => "context fuel layer agreement";
  }

  private void CheckContractContextFuelLayers(IOrigin origin, BoogieStmtListBuilder checking,
    List<(Bpl.Expr Condition, Expression Source)> facts, Expression source, BoogieStmtListBuilder continuation) {
    // Read the already translated statement prefix, including loop invariants.
    // This collects terms, not assumptions. The unconditional layer law is valid
    // for every value of the current implementation locals and heaps, including
    // a value different from the one at the earlier source occurrence.
    var revealFunctions = declarationMapping.Values.Where(function =>
      function.InParams.Any(parameter => parameter.TypedIdent.Name == "$reveal"))
      .Select(function => function.Name).ToHashSet();
    bool EligibleAttributes(Bpl.QKeyValue attributes) => attributes == null ||
      attributes.Params.OfType<Bpl.Expr>().All(Eligible) && EligibleAttributes(attributes.Next);
    bool EligibleTriggers(Bpl.Trigger triggers) => triggers == null ||
      triggers.Tr.All(Eligible) && EligibleTriggers(triggers.Next);
    bool EligibleVariables(IEnumerable<Bpl.Variable> variables) => variables.All(variable =>
      variable.TypedIdent.WhereExpr == null && EligibleAttributes(variable.Attributes));
    bool Eligible(Bpl.Expr expression) => expression switch {
      Bpl.NAryExpr application =>
        (application.Fun is not Bpl.FunctionCall function || !revealFunctions.Contains(function.FunctionName)) &&
        application.Args.All(Eligible),
      Bpl.QuantifierExpr quantifier => quantifier.TypeParameters.Count == 0 &&
        EligibleVariables(quantifier.Dummies) && EligibleAttributes(quantifier.Attributes) &&
        EligibleTriggers(quantifier.Triggers) && Eligible(quantifier.Body),
      Bpl.LetExpr let => EligibleVariables(let.Dummies) && EligibleAttributes(let.Attributes) &&
        let.Rhss.All(Eligible) && Eligible(let.Body),
      // Eligibility may inspect old arguments, but the collector still never
      // descends into an OldExpr to extract an application. A copied application
      // retains each old heap/value argument on both sides of its equality.
      Bpl.OldExpr old => Eligible(old.Expr),
      Bpl.IdentifierExpr or Bpl.LiteralExpr => true,
      _ => false
    };
    var expressions = new List<Bpl.Expr>();
    void Add(Bpl.Expr expression) {
      // Conservatively reject an entire expression containing a reveal-parameter
      // application, even as a nested argument. Never transport proof-local body
      // visibility. Unsupported expression-local scopes are also left alone.
      if (expression != null && Eligible(expression)) { expressions.Add(expression); }
    }
    void GatherList(Bpl.StmtList statements) {
      if (statements == null) { return; }
      if (statements.PrefixCommands != null) {
        foreach (var command in statements.PrefixCommands) { Gather(command); }
      }
      foreach (var block in statements.BigBlocks) {
        foreach (var command in block.simpleCmds) { Gather(command); }
        Gather(block.ec);
      }
    }
    void Gather(object command) {
      switch (command) {
        case Bpl.PredicateCmd predicate:
          Add(predicate.Expr);
          break;
        case Bpl.IfCmd conditional when !contractProofCuts.Contains(conditional):
          Add(conditional.Guard);
          GatherList(conditional.Thn);
          Gather(conditional.ElseIf);
          GatherList(conditional.ElseBlock);
          break;
        case Bpl.WhileCmd loop:
          Add(loop.Guard);
          foreach (var invariant in loop.Invariants) { Add(invariant.Expr); }
          GatherList(loop.Body);
          break;
      }
    }
    // Do not Collect() or modify the original builder or its commands.
    foreach (var command in continuation.Commands) { Gather(command); }
    var equalities = ContractScopedFuelEqualities(origin, expressions, true);
    // Retain every cache-based instance above. Add only signatures missing from
    // that cache; the same eligibility, lexical closure and checked publication
    // rules apply. The original contract-only collectors stay unchanged.
    var additionalFunctions = AdditionalContextFuelFunctions();
    if (additionalFunctions.Count != 0) {
      equalities.AddRange(ContractScopedFuelEqualities(origin, expressions, true, additionalFunctions));
    }
    if (equalities.Count == 0) { return; }
    var agreement = equalities.Aggregate((left, right) => BplAnd(left, right));
    checking.Add(AssertMethodContract(origin, agreement, new ContractContextFuelLayerDescription(), checking.Context));
    facts.Add((agreement, source));
  }

  private sealed class ContractBodyEqualityDescription : ProofObligationDescription {
    public override string SuccessDescription => "available predicate definition agrees";
    public override string FailureDescription => "could not establish the available predicate definition";
    public override string ShortDescription => "predicate body agreement";
  }

  private void CheckContractBodyEqualities(IOrigin origin, BoogieStmtListBuilder checking,
    List<(Bpl.Expr Condition, Expression Source)> facts, PropositionLowering lowering) {
    // An inlined free piece already contains can-call && predicate && body.
    // Check the guarded folding equality before using it. The existing splitter
    // has enforced visibility, SCC height, no_inline and safe substitution.
    if (checking.Context.ContainsHide) { return; }
    var functions = declarationMapping.Where(pair => pair.Key is Function f && !f.IsFuelAware())
      .ToDictionary(pair => pair.Value.Name, pair => pair.Value);
    foreach (var piece in lowering.Pieces.Where(piece => piece.IsOnlyFree)) {
      if (piece.E is not Bpl.NAryExpr {
            Fun: Bpl.BinaryOperator { Op: Bpl.BinaryOperator.Opcode.And }, Args.Count: 2
          } preparation ||
          preparation.Args[0] is not Bpl.NAryExpr { Fun: Bpl.FunctionCall } permission ||
          preparation.Args[1] is not Bpl.NAryExpr {
            Fun: Bpl.BinaryOperator { Op: Bpl.BinaryOperator.Opcode.And }, Args.Count: 2
          } proposition ||
          proposition.Args[0] is not Bpl.NAryExpr { Fun: Bpl.FunctionCall } application ||
          !functions.TryGetValue(((Bpl.FunctionCall)application.Fun).FunctionName, out var function) ||
          ((Bpl.FunctionCall)permission.Fun).FunctionName != function.Name + "#canCall" ||
          application.Args.Count != function.InParams.Count) { continue; }
      var agreement = BplImp(permission, Bpl.Expr.Eq(application, proposition.Args[1]));
      // Keep the existing Boolean heap trigger as a guard and retain the
      // actual reveal flag. Neither guard is granted unconditionally.
      for (var i = function.InParams.Count - 1; i >= 0; i--) {
        if (function.InParams[i].TypedIdent.Type == Predef.HeapType) {
          agreement = BplImp(FunctionCall(origin, BuiltinFunction.IsGoodHeap, null, application.Args[i]), agreement);
        } else if (function.InParams[i].TypedIdent.Name == "$reveal" &&
                   function.InParams[i].TypedIdent.Type == Bpl.Type.Bool) {
          agreement = BplImp(application.Args[i], agreement);
        }
      }
      checking.Add(AssertMethodContract(origin, agreement, new ContractBodyEqualityDescription(), checking.Context));
      facts.Add((agreement, lowering.Source));
    }
  }

  // Do not feed generated proof scaffolding back into context-term collection.
  // Its checks and continuation facts remain in the original statement stream.
  private readonly HashSet<Bpl.IfCmd> contractProofCuts = new(ReferenceEqualityComparer.Instance);

  private void PublishContractProofCut(IOrigin origin, BoogieStmtListBuilder continuation,
    BoogieStmtListBuilder checking, IReadOnlyList<(Bpl.Expr Condition, Expression Source)> facts) {
    // The verification arm checks every fact before becoming unreachable. The
    // continuation arm receives exactly those facts and the justified contract
    // permissions. Both arms stay in the existing VC; no batching marker or
    // visibility scope is introduced.
    if (facts.Count == 0) { return; }
    checking.Add(TrAssumeCmd(origin, Bpl.Expr.False));
    var publication = new BoogieStmtListBuilder(this, options, continuation.Context);
    foreach (var fact in facts) {
      var command = TrAssumeCmd(origin, fact.Condition);
      proofDependencies?.AddProofDependencyId(command, origin,
        new AssumptionDependency(false, "verified contract continuation", fact.Source));
      publication.Add(command);
    }
    var cut = new Bpl.IfCmd(origin, null, checking.Collect(origin), null, publication.Collect(origin));
    contractProofCuts.Add(cut);
    continuation.Add(cut);
  }

  private void CheckMethodPostconditions(MethodOrConstructor method, IOrigin returnOrigin,
    BoogieStmtListBuilder builder, ExpressionTranslator etran) {
    // The declared contract WF procedure establishes permissions in clause order.
    // Check both the assertion-style pieces and the exact original contract
    // formulas before publishing a summary. Rechecking the same contract after
    // that summary creates an unnecessary cut and a different solver context.
    if (assertionOnlyFilter != null) { return; }
    if (!methodPostconditionClauses.TryGetValue(method, out var clauses)) {
      throw new System.InvalidOperationException("Missing original method postcondition checks");
    }
    var continuation = builder;
    builder = new BoogieStmtListBuilder(this, options, continuation.Context);
    var facts = new List<(Bpl.Expr Condition, Expression Source)>();
    foreach (var clause in clauses) {
      var ensures = clause.Source;
      var permission = etran.CanCallAssumptionForVerification(ensures.E);
      builder.Add(TrAssumeCmd(ensures.E.Origin, permission));
      facts.Add((permission, ensures.E));
      var lowering = LowerProposition(builder.Context, ensures.E, etran);
      CheckContractFuelLayers(ensures.E.Origin, builder, facts, ensures.E,
        new[] { lowering.Summary, permission }.Concat(lowering.Pieces.Select(piece => piece.E))
          .Concat(clause.OriginalChecks.Select(original => original.Condition)));
      CheckContractQuantifiedFuelLayers(ensures.E.Origin, builder, facts, ensures.E,
        new[] { lowering.Summary, permission }.Concat(lowering.Pieces.Select(piece => piece.E))
          .Concat(clause.OriginalChecks.Select(original => original.Condition)));
      CheckContractContextFuelLayers(ensures.E.Origin, builder, facts, ensures.E, continuation);
      CheckContractBodyEqualities(ensures.E.Origin, builder, facts, lowering);
      var (error, success) = CustomErrorMessage(ensures.Attributes);
      var description = new EnsuresDescription(ensures.E, error, success);
      foreach (var piece in lowering.Pieces) {
        if (!piece.IsChecked) { continue; }
        var check = piece.E;
        if (piece.Tok.IsInherited(currentModule)) {
          check = BplImp(new Bpl.IdentifierExpr(returnOrigin, "$_reverifyPost", Bpl.Type.Bool), check);
        }
        // Checked procedure ensures publish each proved piece to the continuation.
        // Keep that policy for their local copies; explicit split assertions retain
        // their separate check-and-forget policy.
        builder.Add(AssertMethodContract(ObligationOrigin(returnOrigin, piece.Tok), check, description, builder.Context));
        facts.Add((check, ensures.E));
      }
      foreach (var original in clause.OriginalChecks) {
        builder.Add(AssertMethodContract(ObligationOrigin(returnOrigin, ToDafnyToken(original.tok)), original.Condition,
          description, builder.Context));
        facts.Add((original.Condition, ensures.E));
      }
      Bpl.Expr guard = ensures.E.Origin.IsInherited(currentModule) ||
        lowering.Pieces.Any(piece => piece.IsChecked && piece.Tok.IsInherited(currentModule))
        ? new Bpl.IdentifierExpr(returnOrigin, "$_reverifyPost", Bpl.Type.Bool) : Bpl.Expr.True;
      // Check the exact publication form too. Ordinary assertion publication
      // retains its facts, triggers and fuel without a separate assumed cut.
      var summary = BplImp(guard, lowering.Summary);
      builder.Add(AssertMethodContract(ObligationOrigin(returnOrigin, ensures.E.Origin),
        summary, description, builder.Context));
      facts.Add((summary, ensures.E));
    }
    PublishContractProofCut(returnOrigin, continuation, builder, facts);
  }

  private Bpl.Expr HigherOrderRequirement(IOrigin origin, int arity, IEnumerable<Bpl.Expr> arguments) =>
    FunctionCall(origin, Requires(arity), Bpl.Type.Bool, arguments.ToList());

  private Bpl.Expr AllocationObligation(IOrigin origin, Bpl.Expr value, Type type,
    ExpressionTranslator etran) {
    var legacy = GetWhereClause(origin, value, type, etran, ISALLOC, true);
    if (legacy == null || !options.Get(CommonOptionBag.ConsistentObligationChecks)) {
      return legacy;
    }
    return BplAnd(legacy, ExplicitAllocationPredicate(origin, value, type, etran.HeapExpr));
  }

  private Bpl.Expr ExplicitAllocationPredicate(IOrigin origin, Bpl.Expr value, Type type, Bpl.Expr heap) =>
    MkIsAllocBox(BoxIfNecessary(origin, value, type), type, heap);

  public partial class ExpressionTranslator {
    internal ExpressionTranslator CloneForObligation() =>
      CloneExpressionTranslator(this, BoogieGenerator, Predef, HeapExpr, This,
        applyLimited_CurrentFunction, layerInterCluster, layerIntraCluster,
        readsFrame, modifiesFrame, stripLits);

    internal ExpressionTranslator WithVerificationOldHeap(Bpl.Expr oldHeap) {
      var clone = CloneForObligation();
      var old = new ExpressionTranslator(BoogieGenerator, Predef, oldHeap, This,
        applyLimited_CurrentFunction, layerInterCluster, layerIntraCluster, scope,
        readsFrame, modifiesFrame, stripLits);
      old.oldEtran = old;
      clone.oldEtran = old;
      return clone;
    }
  }
}
