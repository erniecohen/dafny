// Resource accounting is not a logical premise: over-budget input is rejected before verification.
module BitvectorResources {
  import opened Ast
  import Types

  function TypeCost(typ: Type): nat { if typ.BitvectorType? then typ.width else 0 }
  function VariableCost(v: Variable): nat { TypeCost(v.typ) }
  function AutoInvariantCost(v: AutoInvVariable): nat
    reads v
  {
    if v.maybeAutoInv.Some? then ExprCost(v.maybeAutoInv.value) else 0
  }
  function ParametersCost(vv: seq<PParameter>): nat
    reads vv
  {
    if vv == [] then 0 else VariableCost(vv[0]) + AutoInvariantCost(vv[0]) + ParametersCost(vv[1..])
  }
  function VariablesCost(vv: seq<Variable>): nat {
    if vv == [] then 0 else VariableCost(vv[0]) + VariablesCost(vv[1..])
  }
  function ExprCost(expr: Expr): nat {
    TypeCost(expr.ExprType()) + match expr {
      case BLiteral(_) | ILiteral(_) | RLiteral(_, _) | BvLiteral(_, _) | CustomLiteral(_, _) | IdExpr(_) => 0
      case OperatorExpr(_, args) => ExprsCost(args)
      case FunctionCallExpr(_, args) => ExprsCost(args)
      case LabeledExpr(_, body) => ExprCost(body)
      case LetExpr(v, rhs, body) => VariableCost(v) + ExprCost(rhs) + ExprCost(body)
      case QuantifierExpr(_, vv, patterns, body) =>
        VariablesCost(vv) + PatternsCost(patterns) + ExprCost(body)
      case ClosureExpr(_, _, _, _) => 0 // The library rejects every closure before this accounting pass.
    }
  }
  function ExprsCost(exprs: seq<Expr>): nat {
    if exprs == [] then 0 else ExprCost(exprs[0]) + ExprsCost(exprs[1..])
  }
  function PatternsCost(patterns: seq<Pattern>): nat {
    if patterns == [] then 0 else ExprsCost(patterns[0].exprs) + PatternsCost(patterns[1..])
  }
  function AExprCost(ae: AExpr): nat
    reads *
  {
    match ae case AExpr(e, _) => ExprCost(e) case AAssertion(s) => StmtCost(s)
  }
  function AExprsCost(ae: seq<AExpr>): nat
    reads *
  {
    if ae == [] then 0 else AExprCost(ae[0]) + AExprsCost(ae[1..])
  }
  function ArgumentsCost(args: seq<CallArgument>): nat {
    if args == [] then 0 else
    (match args[0] case InArgument(e) => ExprCost(e) case OutgoingArgument(_, v) => VariableCost(v)) + ArgumentsCost(args[1..])
  }
  function StmtCost(stmt: Stmt): nat
    reads *
  {
    match stmt
    case VarDecl(v, initial, body) => VariableCost(v) + AutoInvariantCost(v) + (if initial.Some? then ExprCost(initial.value) else 0) + StmtCost(body)
    case Assign(v, rhs) => VariableCost(v) + ExprCost(rhs)
    case Reinit(vv) => VariablesCost(vv)
    case Block(stmts) => StmtsCost(stmts)
    case Choose(stmts) => StmtsCost(stmts)
    case Call(_, args) => ArgumentsCost(args)
    case Check(cond, _) => ExprCost(cond)
    case Assert(cond, _) => ExprCost(cond)
    case Reach(cond, _) => ExprCost(cond)
    case Assume(cond) => ExprCost(cond)
    case Probe(cond) => ExprCost(cond)
    case AForall(v, body) => VariableCost(v) + StmtCost(body)
    case Loop(invariants, body) => AExprsCost(invariants) + StmtCost(body)
    case LabeledStmt(_, body) => StmtCost(body)
    case Exit(_) => 0
  }
  function StmtsCost(stmts: seq<Stmt>): nat
    reads *
  {
    if stmts == [] then 0 else StmtCost(stmts[0]) + StmtsCost(stmts[1..])
  }
  function FunctionsCost(functions: seq<Function>): nat
    reads functions
  {
    if functions == [] then 0 else
    TypeCost(functions[0].ResultType) + VariablesCost(functions[0].Parameters) +
    (if functions[0].Definition.Some? then ExprsCost(functions[0].Definition.value.when) + ExprCost(functions[0].Definition.value.body) else 0) +
    FunctionsCost(functions[1..])
  }
  function AxiomsCost(axioms: seq<Axiom>): nat {
    if axioms == [] then 0 else ExprCost(axioms[0].Expr) + AxiomsCost(axioms[1..])
  }
  function ProceduresCost(procedures: seq<Procedure>): nat
    reads *
  {
    if procedures == [] then 0 else
    ParametersCost(procedures[0].Parameters) + AExprsCost(procedures[0].Pre) + AExprsCost(procedures[0].Post) +
    (if procedures[0].Body.Some? then StmtCost(procedures[0].Body.value) else 0) + ProceduresCost(procedures[1..])
  }
  function ProgramCost(program: Program): nat
    reads *
  {
    FunctionsCost(program.functions) + AxiomsCost(program.axioms) + ProceduresCost(program.procedures)
  }
}
