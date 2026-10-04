module BitvectorWorkerTests {
  import opened Std.Wrappers
  import opened Basics
  import Raw = RawAst
  import Types
  import Ast
  import Parser
  import Resolver
  import TypeChecker
  import Printer
  import ResolvedPrinter
  import SolverExpr
  import RSolvers
  import B3Library
  import SolverConfiguration
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
  method EveryTypedPrimitiveTextRoundTrips() {
    var inputs := [
      "#bvand(3, #bv(7, 3), #bv(3, 3)) == #bv(3, 3)",
      "#bvor(3, #bv(4, 3), #bv(3, 3)) == #bv(7, 3)",
      "#bvxor(3, #bv(7, 3), #bv(3, 3)) == #bv(4, 3)",
      "#bvnot(1, #bv(1, 1)) == #bv(0, 1)",
      "#bvadd(7, #bv(127, 7), #bv(1, 7)) == #bv(0, 7)",
      "#bvsub(7, #bv(0, 7), #bv(1, 7)) == #bv(127, 7)",
      "#bvmul(7, #bv(64, 7), #bv(2, 7)) == #bv(0, 7)",
      "#bvudiv(7, #bv(127, 7), #bv(0, 7)) == #bv(127, 7)",
      "#bvurem(7, #bv(127, 7), #bv(0, 7)) == #bv(127, 7)",
      "#bvult(7, #bv(0, 7), #bv(127, 7))",
      "#bvule(7, #bv(127, 7), #bv(127, 7))",
      "#bvshl(7, #bv(1, 7), #bv(7, 7)) == #bv(0, 7)",
      "#bvlshr(7, #bv(64, 7), #bv(127, 7)) == #bv(0, 7)",
      "#extract(3, 2, 5, #bv(127, 7)) == #bv(7, 3)",
      "#concat(7, #bv(3, 3), #bv(15, 4)) == #bv(63, 7)",
      "#int2bv(4096, -1) == #int2bv(4096, -1)",
      "#bv2int(67, #bv(73786976294838206464, 67)) == 73786976294838206464"
    ];
    for i := 0 to |inputs| {
      var expr := Resolve(inputs[i]);
      var text := expr.ToString();
      var reparsed := Resolve(text);
      expect reparsed.ToString() == text;
    }
  }

  @Test
  method NativeWordSMTPrintingKeepsWidthAndHighBits() {
    var word := Types.Word(73786976294838206464, 67);
    expect word.Valid();
    var literal := SolverExpr.SExpr.Bitvector(word);
    expect literal.ToString() == "(_ bv73786976294838206464 67)";
    expect SolverExpr.SType.SBitvector(67).ToSExpr().ToString() == "(_ BitVec 67)";
    expect SolverExpr.SType.SBitvector(67).ToString() == "#bv67";
  }

  @Test
  method IndexedSMTHeadsComeFromTypedMetadata() {
    var word := RSolvers.RExpr.Bitvector(Types.Word(127, 7));
    var extract := RSolvers.RExpr.FuncAppl(
      RSolvers.ROperator.NativeBitvector(Raw.Operator.Bv(Raw.BitvectorOperator.BvExtract, 3, 2, 5)), [word]);
    expect extract.ToSExpr(map[]).ToString() == "((_ extract 4 2) (_ bv127 7))";
    expect extract.ToString() == "#extract(3, 2, 5, #bv(127, 7))";
    var conversion := RSolvers.RExpr.FuncAppl(
      RSolvers.ROperator.NativeBitvector(Raw.Operator.Bv(Raw.BitvectorOperator.IntToBv, 7)), [RSolvers.RExpr.Integer(-1)]);
    expect conversion.ToSExpr(map[]).ToString() == "((_ int2bv 7) (- 1))";
    var reparsed := Resolve(extract.ToString() + " == #bv(7, 3)");
    expect reparsed.OperatorExpr?;
  }

  @Test
  method LiteralBoundsAreCheckedAtRawResolution() {
    var invalid := ["#bv(0, 0)", "#bv(0, -1)", "#bv(0, 4097)", "#bv(-1, 7)", "#bv(128, 7)"];
    for i := 0 to |invalid| {
      var raw := Parse("procedure sP() { check " + invalid[i] + " == " + invalid[i] + " }");
      var result, _ := Resolver.Resolve(raw, []);
      expect result.Failure?;
    }
    expect Types.BitvectorLiteralValid(1, 1) && !Types.BitvectorLiteralValid(2, 1);
    expect Types.WordBound(3) == 8 && Types.WordBound(7) == 128;
    expect Types.BitvectorLiteralValid(0, 4096);
  }

  @Test
  method WidthNamesAreCanonicalAndTheNamespaceIsReserved() {
    expect Types.ParseBitvectorWidth("#bv1") == Some(1);
    expect Types.ParseBitvectorWidth("#bv4096") == Some(4096);
    var invalid := ["#bv0", "#bv01", "#bv-1", "#bv4097", "#bv10000000000000000000", "#bv7x"];
    for i := 0 to |invalid| {
      expect Types.ParseBitvectorWidth(invalid[i]).None?;
      var raw := Raw.Program({}, [], [], [], [], [], [
        Raw.Procedure("sP", [Raw.PParameter("sX", Raw.In, invalid[i], None)], [], [], Some(Raw.Block([])))]);
      var rejected, _ := Resolver.Resolve(raw, []);
      expect rejected.Failure?;
    }
    var declared := Raw.Program({}, [], [Raw.TypeDecl("#bv7", None)], [], [], [], []);
    var rejectedDeclaration, _ := Resolver.Resolve(declared, []);
    expect rejectedDeclaration.Failure?;
  }

  @Test
  method MalformedPrimitiveParametersFailAtResolution() {
    var invalid := [
      "#bvadd(0, #bv(0, 1), #bv(0, 1))",
      "#bvadd(4097, #bv(0, 1), #bv(0, 1))",
      "#extract(3, 5, 2, #bv(0, 7))",
      "#extract(2, 2, 5, #bv(0, 7))"
    ];
    for i := 0 to |invalid| {
      var raw := Parse("procedure sP() { check " + invalid[i] + " == " + invalid[i] + " }");
      var rejected, _ := Resolver.Resolve(raw, []);
      expect rejected.Failure?;
    }
  }

  @Test
  method MixedWidthsAndWrongExtractionConcatOrConversionFailTyping() {
    var invalid := [
      "#bvadd(7, #bv(0, 7), #bv(0, 3)) == #bv(0, 7)",
      "#extract(3, 5, 8, #bv(0, 7)) == #bv(0, 3)",
      "#concat(7, #bv(0, 3), #bv(0, 3)) == #bv(0, 7)",
      "#int2bv(7, #real(1, 1)) == #bv(0, 7)",
      "#bv2int(7, #bv(0, 3)) == 0"
    ];
    for i := 0 to |invalid| {
      var raw := Parse("procedure sP() { check " + invalid[i] + " }");
      var resolved, _ := Resolver.Resolve(raw, []);
      expect resolved.Success?;
      var checked := TypeChecker.TypeCheck(resolved.value);
      expect checked.IsFailure();
    }
  }

  @Test
  method RawAndResolvedPrintersExposeTheSameNativeForms() {
    var raw := Parse("procedure sP() { check #bvand(7, #bv(127, 7), #bv(64, 7)) == #bv(64, 7) }");
    var rawBody := raw.procedures[0].body.value;
    expect rawBody.Block? && rawBody.stmts[0].Check?;
    Printer.Expression(rawBody.stmts[0].cond);
    print "\n";
    var resolved := Resolve("#bvand(7, #bv(127, 7), #bv(64, 7)) == #bv(64, 7)");
    ResolvedPrinter.Expression(resolved);
    print "\n";
    expect resolved.ToString() == "#bvand(7, #bv(127, 7), #bv(64, 7)) == #bv(64, 7)";
  }

  @Test
  method AggregateNativeBitBudgetRejectsBeforeSolverStartup() {
    var parameters := seq(1025, (i: int) => Raw.PParameter("sP" + Int2String(i), Raw.In, "#bv4096", None));
    var raw := Raw.Program({}, [], [], [], [], [], [
      Raw.Procedure("sP", parameters, [], [], Some(Raw.Block([Raw.Check(Raw.BLiteral(true))])))]);
    var absent := SolverConfiguration.Configuration("b3-absent-solver", [], 1000, 0, 1048576);
    var result := B3Library.CheckAndVerify(raw, "sP", absent);
    expect !result.complete && result.error.Some? && result.attempts == [];
    expect "unsupported: aggregate native bitvector cost" <= result.error.value;
  }

  @Test
  method AutomaticInvariantExpressionsCountTowardTheSameBudget() {
    var value := Raw.BvLiteral(0, 4096);
    var autoInv := Raw.OperatorExpr(Raw.Eq, [value, value]);
    var parameters := seq(513, (i: int) => Raw.PParameter("sP" + Int2String(i), Raw.In, "bool", Some(autoInv)));
    var raw := Raw.Program({}, [], [], [], [], [], [
      Raw.Procedure("sP", parameters, [], [], Some(Raw.Block([Raw.Check(Raw.BLiteral(true))])))]);
    var absent := SolverConfiguration.Configuration("b3-absent-solver", [], 1000, 0, 1048576);
    var result := B3Library.CheckAndVerify(raw, "sP", absent);
    expect !result.complete && result.error.Some? && result.attempts == [];
    expect "unsupported: aggregate native bitvector cost" <= result.error.value;
  }
}
