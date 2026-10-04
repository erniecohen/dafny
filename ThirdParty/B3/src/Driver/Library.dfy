module B3Library {
  import opened Std.Wrappers
  import Raw = RawAst
  import Resolver
  import TypeChecker
  import StaticConsistency
  import Verifier
  import VerificationResults
  import SolverConfiguration

  export
    provides CheckAndVerify
    provides Raw, VerificationResults, SolverConfiguration, Wrappers

  // External construction always passes through resolution, type checking, and
  // consistency checking. No client-supplied checked flag crosses this boundary.
  method CheckAndVerify(raw: Raw.Program, selectedProcedure: string,
    configuration: SolverConfiguration.Configuration) returns (r: VerificationResults.UnitResult)
  {
    if !configuration.Valid() {
      return VerificationResults.UnitResult(selectedProcedure, [], false, Some("invalid solver configuration"));
    }
    if raw.signatureTypes != {} || raw.domains != [] ||
      exists typ <- raw.types :: typ.domainInstantiation.Some?
    {
      return VerificationResults.UnitResult(selectedProcedure, [], false,
        Some("unsupported: domains, domain instantiations, and signature types"));
    }
    if !SupportedProgram(raw) {
      return VerificationResults.UnitResult(selectedProcedure, [], false,
        Some("unsupported: closure, reachability, custom literal, or unsafe SMT identifier"));
    }
    var resolution, _ := Resolver.Resolve(raw, []);
    if resolution.Failure? {
      return VerificationResults.UnitResult(selectedProcedure, [], false, Some("invalid input: " + resolution.error));
    }
    var program := resolution.value;
    var checked := TypeChecker.TypeCheck(program);
    if checked.IsFailure() {
      return VerificationResults.UnitResult(selectedProcedure, [], false, Some("invalid input: " + checked.error));
    }
    checked := StaticConsistency.CheckConsistent(program);
    if checked.IsFailure() {
      return VerificationResults.UnitResult(selectedProcedure, [], false, Some("invalid input: " + checked.error));
    }
    r := Verifier.VerifySelected(program, selectedProcedure, configuration);
  }

  predicate SafeSymbol(name: string) {
    name != "" && forall c <- name ::
      'a' <= c <= 'z' || 'A' <= c <= 'Z' || '0' <= c <= '9' || c in "$%_!?@^~&*+-=<>./#"
  }

  predicate SupportedProgram(raw: Raw.Program) {
    && (forall typ <- raw.types :: SafeSymbol(typ.name))
    && (forall tagger <- raw.taggers :: SafeSymbol(tagger.name) && SafeSymbol(tagger.typ))
    && (forall func <- raw.functions ::
      && SafeSymbol(func.name) && SafeSymbol(func.resultType)
      && (func.tag.Some? ==> SafeSymbol(func.tag.value))
      && (forall p <- func.parameters :: SafeSymbol(p.name) && SafeSymbol(p.typ))
      && (func.definition.Some? ==> (SupportedExpression(func.definition.value.body) &&
          (forall e <- func.definition.value.when :: SupportedExpression(e)))))
    && (forall axiom <- raw.axioms :: (SupportedExpression(axiom.expr) &&
      (forall name <- axiom.explains :: SafeSymbol(name))))
    && (forall proc <- raw.procedures ::
      && SafeSymbol(proc.name)
      && (forall p <- proc.parameters :: (SafeSymbol(p.name) && SafeSymbol(p.typ) &&
        (p.optionalAutoInv.Some? ==> SupportedExpression(p.optionalAutoInv.value))))
      && (forall ae <- proc.pre + proc.post :: SupportedAExpr(ae))
      && (proc.body.Some? ==> SupportedStatement(proc.body.value)))
  }

  predicate SupportedExpression(expr: Raw.Expr) {
    match expr
    case BLiteral(_) | ILiteral(_) => true
    case CustomLiteral(_, _) | ClosureExpr(_, _, _, _) => false
    case IdExpr(name, _) => SafeSymbol(name)
    case OperatorExpr(_, args) => forall e <- args :: SupportedExpression(e)
    case FunctionCallExpr(name, args) => SafeSymbol(name) && forall e <- args :: SupportedExpression(e)
    case LabeledExpr(name, body) => SafeSymbol(name) && SupportedExpression(body)
    case LetExpr(name, optionalType, rhs, body) => SafeSymbol(name) &&
      (optionalType.Some? ==> SafeSymbol(optionalType.value)) && SupportedExpression(rhs) && SupportedExpression(body)
    case QuantifierExpr(_, bindings, patterns, body) =>
      && (forall b <- bindings :: SafeSymbol(b.name) && SafeSymbol(b.typ))
      && (forall pattern <- patterns :: forall e <- pattern.exprs :: SupportedExpression(e))
      && SupportedExpression(body)
  }

  predicate SupportedAExpr(ae: Raw.AExpr) {
    match ae
    case AExpr(e) => SupportedExpression(e)
    case AAssertion(s) => SupportedStatement(s)
  }

  predicate SupportedStatement(stmt: Raw.Stmt) {
    match stmt
    case VarDecl(v, init, body) => SafeSymbol(v.name) &&
      (v.optionalType.Some? ==> SafeSymbol(v.optionalType.value)) &&
      (v.optionalAutoInv.Some? ==> SupportedExpression(v.optionalAutoInv.value)) &&
      (init.Some? ==> SupportedExpression(init.value)) && SupportedStatement(body)
    case Assign(name, rhs) => SafeSymbol(name) && SupportedExpression(rhs)
    case Reinit(names) => forall name <- names :: SafeSymbol(name)
    case Block(stmts) => forall s <- stmts :: SupportedStatement(s)
    case Call(name, args) => SafeSymbol(name) && forall arg <- args :: SupportedExpression(arg.arg)
    case Check(_) | Assume(_) | Assert(_) => SupportedExpression(stmt.cond)
    case Probe(e) => SupportedExpression(e)
    case Reach(_) => false
    case AForall(name, typ, body) => SafeSymbol(name) && SafeSymbol(typ) && SupportedStatement(body)
    case Choose(branches) => forall branch <- branches :: SupportedStatement(branch)
    case If(cond, thn, els) => SupportedExpression(cond) && SupportedStatement(thn) && SupportedStatement(els)
    case IfCase(cases) => forall branch <- cases :: SupportedExpression(branch.cond) && SupportedStatement(branch.body)
    case Loop(invariants, body) => (forall ae <- invariants :: SupportedAExpr(ae)) && SupportedStatement(body)
    case LabeledStmt(lbl, body) => SafeSymbol(lbl) && SupportedStatement(body)
    case Exit(optionalLabel) => optionalLabel.Some? ==> SafeSymbol(optionalLabel.value)
    case Return => true
  }
}
