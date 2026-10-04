module RealWorkerTests {
  import opened Std.Wrappers
  import Raw = RawAst
  import Ast
  import Parser
  import Resolver
  import TypeChecker
  import Printer
  import ResolvedPrinter
  import SolverExpr
  import RSolvers
  import SB = Std.Parsers.StringBuilders

  method Parse(text: string) returns (raw: Raw.Program) {
    var parsed := SB.Apply(Parser.TopLevel, text);
    expect parsed.ParseSuccess?;
    return parsed.result;
  }

  method Resolve(text: string) returns (expr: Ast.Expr) {
    var raw := Parse("procedure sP() { check " + text + " }");
    var resolved, _ := Resolver.Resolve(raw, []);
    expect resolved.Success?;
    var program := resolved.value;
    var checked := TypeChecker.TypeCheck(program);
    expect checked.Pass?;
    expect |program.procedures| == 1 && program.procedures[0].Body.Some?;
    // ResolveProcedureBody installs one synthetic return-label wrapper around the parsed body.
    // Require that exact shape before extracting the single checked expression.
    var returned := program.procedures[0].Body.value;
    expect returned.LabeledStmt? && returned.lbl.Name == "return";
    var body := returned.body;
    expect body.Block? && |body.stmts| == 1 && body.stmts[0].Check?;
    return body.stmts[0].cond;
  }

  @Test
  method NativeRealTextRoundTripsWithoutLosingTheLiteralOrCoercion() {
    var expr := Resolve("#real(-13, 10) == #to_real(#to_int(#real(-13, 10)))");
    var text := expr.ToString();
    var reparsed := Resolve(text);
    expect reparsed.ToString() == text;
    expect expr.OperatorExpr? && expr.args[0].RLiteral?;
    expect expr.args[0].numerator == -13 && expr.args[0].denominator == 10;
  }

  @Test
  method RealDivisionPrecedenceRoundTrips() {
    var expr := Resolve("#real(1, 3) + #real(2, 3) / (#real(5, 7) - #real(1, 7)) == #real(3, 2)");
    var text := expr.ToString();
    var reparsed := Resolve(text);
    expect reparsed.ToString() == text;
  }

  @Test
  method RationalSMTPrintingMakesRealSortAndSignsExplicit() {
    var expression := SolverExpr.SExpr.Rational(-13, 10);
    expect expression.ToString() == "(/ (to_real (- 13)) (to_real 10))";
  }

  @Test
  method SolverDiagnosticsPreserveNativeCoercionForms() {
    var literal := RSolvers.RExpr.Rational(-13, 10);
    var floor := RSolvers.RExpr.FuncAppl(RSolvers.ROperator.BuiltInOperator("to_int"), [literal]);
    var embed := RSolvers.RExpr.FuncAppl(RSolvers.ROperator.BuiltInOperator("to_real"), [floor]);
    expect embed.ToString() == "#to_real(#to_int(#real(-13, 10)))";
    var expr := Resolve(embed.ToString() + " == #real(-2, 1)");
    expect expr.ToString() == "#to_real(#to_int(#real(-13, 10))) == #real(-2, 1)";
  }

  @Test
  method ResolutionRejectsANonpositiveRawDenominator() {
    var zero := Parse("procedure sP() { check #real(1, 0) == #real(1, 1) }");
    var rejectedZero, _ := Resolver.Resolve(zero, []);
    expect rejectedZero.Failure?;
    var negative := Parse("procedure sP() { check #real(1, -1) == #real(1, 1) }");
    var rejectedNegative, _ := Resolver.Resolve(negative, []);
    expect rejectedNegative.Failure?;
  }

  @Test
  method RawAndResolvedPrintersExposeTheSameNewForms() {
    var raw := Parse("procedure sP() { check #to_int(#real(-13, 10)) == -2 }");
    var rawBody := raw.procedures[0].body.value;
    expect rawBody.Block? && |rawBody.stmts| == 1 && rawBody.stmts[0].Check?;
    Printer.Expression(rawBody.stmts[0].cond);
    print "\n";
    var expr := Resolve("#to_int(#real(-13, 10)) == -2");
    ResolvedPrinter.Expression(expr);
    print "\n";
    expect expr.ToString() == "#to_int(#real(-13, 10)) == -2";
  }
}
