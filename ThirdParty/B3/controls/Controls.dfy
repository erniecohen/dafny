module CheckedB3Controls {
  import opened Std.Wrappers
  import opened Ast
  import Raw = RawAst
  import I = Incarnations
  import SolverExpr
  import RSolvers
  import TypeChecker
  import Verifier
  import AstValid
  import OSProcesses
  import ExternalSolvers

  method Main(args: seq<string>) {
    expect |args| >= 2;
    var mode := args[1];
    var mapping := I.DeclMappings(map[], map[]);
    var inc := I.Incarnations.Empty(mapping);
    var v := new LocalVariable("x", true, BoolType);
    expect v.IsMutable();
    var bad := OperatorExpr(Operator.LogicalNot, []);
    if mode == "type-check-parameter" {
      var p := new PParameter("x", Raw.In, BoolType, None);
      p.maybeAutoInv := Some(bad);
      var proc := new Procedure("P", [p], [], []);
      var program := Program([], [], [], [proc]);
      expect program.WellFormed();
      var result := TypeChecker.TypeCheck(program);
      expect result.Fail?;
    } else if mode == "type-check-local" {
      v.maybeAutoInv := Some(bad);
      var proc := new Procedure("P", [], [], []);
      proc.Body := Some(VarDecl(v, None, Block([])));
      var program := Program([], [], [], [proc]);
      expect program.WellFormed();
      var result := TypeChecker.TypeCheck(program);
      expect result.Fail?;
    } else if mode == "missing-type" {
      var typ := new TypeDecl("Missing");
      var _ := mapping.Type2SType(UserType(typ));
    } else if mode == "missing-get" {
      var _ := inc.Get(v);
    } else if mode == "missing-variable" {
      var _ := inc.SubstituteVariable(v);
    } else if mode == "missing-function" {
      var f := new Function("Missing", [], BoolType, None);
      var _ := inc.Substitute(FunctionCallExpr(f, []));
    } else if mode == "missing-let" {
      var _ := inc.Substitute(LetExpr(v, BLiteral(true), BLiteral(false)));
    } else if mode == "missing-quantifier" {
      var _ := inc.Substitute(QuantifierExpr(true, [v], [], BLiteral(true)));
    } else if mode == "closure" {
      var _ := inc.Substitute(ClosureExpr([], "result", BoolType, []));
    } else if mode == "positive-substitution" {
      var st := new SolverExpr.SConstant("x", SolverExpr.SBool);
      inc := inc.Set(v, st);
      var value := inc.Get(v);
      expect value == st;
      var expr := inc.Substitute(IdExpr(v));
      expect expr == RSolvers.RExpr.Id(st);
      var w := new LocalVariable("y", false, BoolType);
      expr := inc.REval(LetExpr(w, BLiteral(true), IdExpr(w)));
      expect expr.LetExpr?;
      var t := mapping.Type2SType(BoolType);
      expect t == SolverExpr.SBool;
    } else if mode == "verifier-parameter" {
      var p := new PParameter("x", Raw.In, BoolType, None);
      p.maybeAutoInv := Some(bad);
      var proc := new Procedure("P", [p], [], []);
      var program := Program([], [], [], [proc]);
      expect AstValid.Program(program);
      Verifier.Verify(program, map[]);
    } else if mode == "verifier-local" || mode == "verifier-loop" {
      v.maybeAutoInv := Some(bad);
      var proc := new Procedure("P", [], [], []);
      if mode == "verifier-local" {
        proc.Body := Some(VarDecl(v, None, Block([])));
      } else {
        proc.Body := Some(VarDecl(v, Some(BLiteral(false)), Loop([], Assign(v, BLiteral(true)))));
      }
      var program := Program([], [], [], [proc]);
      expect AstValid.Program(program);
      Verifier.Verify(program, map[]);
    } else if mode == "verifier-call" {
      var p := new PParameter("x", Raw.In, BoolType, None);
      var callee := new Procedure("Bad", [p, p], [], []);
      var proc := new Procedure("P", [], [], []);
      proc.Body := Some(Call(callee, [InArgument(BLiteral(true)), InArgument(BLiteral(true))]));
      var program := Program([], [], [], [proc]);
      expect AstValid.Program(program);
      Verifier.Verify(program, map[]);
    } else if mode == "response-balanced" || mode == "response-limit" {
      expect |args| == 4;
      var process :- expect OSProcesses.OSProcess.Create(args[2], args[3]);
      var result := ExternalSolvers.ReadBalancedParentheses(process, 2);
      if mode == "response-balanced" {
        expect result.Success? && result.value == "(\n)\n";
      } else {
        expect result.Failure? && result.error == "solver response line limit exceeded";
      }
    } else {
      expect false, "unknown control";
    }
    print "CONTROL PASSED\n";
  }
}
