using System.Numerics;
using DafnyB3Protocol;
using Dafny;
using Std.Wrappers;
using NormalizedProgram = DafnyB3Protocol.Program;
using Expr = RawAst._IExpr;
using Stmt = RawAst._IStmt;
using RuneString = Dafny.ISequence<Dafny.Rune>;

namespace DafnyB3Host;

/// <summary>Only constructs RawAst; validation and elaboration always occur in B3Library.</summary>
public static class RawAstBuilder {
  public static RawAst._IProgram Build(NormalizedProgram program) {
    var body = Statement(program.Unit.Body);
    foreach (var variable in program.Unit.Variables.Reverse()) {
      body = RawAst.Stmt.create_VarDecl(RawAst.Variable.create(S(variable.Name), true,
        Some(S(variable.Type)), None<Expr>()), None<Expr>(), body);
    }
    var procedure = RawAst.Procedure.create(S(program.Unit.Name), Seq(Array.Empty<RawAst._IPParameter>()),
      Seq(Array.Empty<RawAst._IAExpr>()), Seq(Array.Empty<RawAst._IAExpr>()), Some(body));
    return RawAst.Program.create_Program(Dafny.Set<RuneString>.Empty, Seq(Array.Empty<RawAst._IDomain>()),
      Seq(program.Types.Select(type => RawAst.TypeDecl.create(S(type), None<RawAst._IDomainInstantiation>()))),
      Seq(Array.Empty<RawAst._ITagger>()),
      Seq(program.Functions.Select(function => RawAst.Function.create(S(function.Name),
        Seq(function.Parameters.Select(parameter => RawAst.FParameter.create(S(parameter.Name), false, S(parameter.Type)))),
        S(function.ResultType), None<RuneString>(), None<RawAst._IFunctionDefinition>()))),
      Seq(program.Axioms.Select(axiom => RawAst.Axiom.create(Seq(axiom.Explains.Select(S)), Expression(axiom.Condition)))),
      Seq(new[] { procedure }));
  }
  public static Expr Expression(DafnyB3Protocol.Expression expression) => expression switch {
    BooleanLiteral literal => RawAst.Expr.create_BLiteral(literal.Value),
    IntegerLiteral literal => RawAst.Expr.create_ILiteral(BigInteger.Parse(literal.Value, System.Globalization.CultureInfo.InvariantCulture)),
    RationalLiteral literal => Rational(literal),
    BitvectorLiteral literal => RawAst.Expr.create_BvLiteral(ProtocolValidation.ParseBitvectorLiteral(literal), literal.Width),
    BitvectorOperation operation => Bitvector(operation),
    Variable variable => RawAst.Expr.create_IdExpr(S(variable.Name), false),
    Application application => RawAst.Expr.create_FunctionCallExpr(S(application.Name), Seq(application.Arguments.Select(Expression))),
    Operation operation => RawAst.Expr.create_OperatorExpr(Operator(operation.Operator), Seq(operation.Arguments.Select(Expression))),
    Quantifier quantifier => RawAst.Expr.create_QuantifierExpr(quantifier.Universal,
      Seq(quantifier.Bindings.Select(binding => RawAst.Binding.create(S(binding.Name), S(binding.Type)))),
      Seq(quantifier.Patterns.Select(pattern => (ISequence<Expr>)Seq(pattern.Select(Expression)))), Expression(quantifier.Body)),
    Let let => RawAst.Expr.create_LetExpr(S(let.Binding.Name), Some(S(let.Binding.Type)), Expression(let.Value), Expression(let.Body)),
    Label label => RawAst.Expr.create_LabeledExpr(S(label.Name), Expression(label.Body)),
    _ => throw new InvalidDataException("Unknown normalized expression")
  };
  public static Stmt Statement(DafnyB3Protocol.Statement statement) => statement switch {
    Block block => RawAst.Stmt.create_Block(Seq(block.Statements.Select(Statement))),
    Assign assign => RawAst.Stmt.create_Assign(S(assign.Variable), Expression(assign.Value)),
    Havoc havoc => RawAst.Stmt.create_Reinit(Seq(havoc.Variables.Select(S))),
    Check check => check.Learn
      ? RawAst.Stmt.create_Assert(RawAst.Expr.create_LabeledExpr(S(check.ObligationId), Expression(check.Condition)))
      : RawAst.Stmt.create_Check(RawAst.Expr.create_LabeledExpr(S(check.ObligationId), Expression(check.Condition))),
    Assume assume => RawAst.Stmt.create_Assume(Expression(assume.Condition)),
    Choice choice => RawAst.Stmt.create_Choose(Seq(choice.Branches.Select(Statement))),
    Conditional conditional => RawAst.Stmt.create_If(Expression(conditional.Condition), Statement(conditional.Then), Statement(conditional.Else)),
    Loop loop => RawAst.Stmt.create_Loop(Seq(Array.Empty<RawAst._IAExpr>()), Statement(loop.Body)),
    Labeled labeled => RawAst.Stmt.create_LabeledStmt(S(labeled.Name), Statement(labeled.Body)),
    Exit exit => RawAst.Stmt.create_Exit(Some(S(exit.Label))),
    Return => RawAst.Stmt.create_Return(),
    _ => throw new InvalidDataException("Unknown normalized statement")
  };
  private static RawAst._IOperator Operator(DafnyB3Protocol.Operator operation) => operation switch {
    DafnyB3Protocol.Operator.IfThenElse => RawAst.Operator.create_IfThenElse(),
    DafnyB3Protocol.Operator.Equiv => RawAst.Operator.create_Equiv(),
    DafnyB3Protocol.Operator.Implies => RawAst.Operator.create_LogicalImp(),
    DafnyB3Protocol.Operator.And => RawAst.Operator.create_LogicalAnd(),
    DafnyB3Protocol.Operator.Or => RawAst.Operator.create_LogicalOr(),
    DafnyB3Protocol.Operator.Equal => RawAst.Operator.create_Eq(),
    DafnyB3Protocol.Operator.NotEqual => RawAst.Operator.create_Neq(),
    DafnyB3Protocol.Operator.Less => RawAst.Operator.create_Less(),
    DafnyB3Protocol.Operator.LessEqual => RawAst.Operator.create_AtMost(),
    DafnyB3Protocol.Operator.Add => RawAst.Operator.create_Plus(),
    DafnyB3Protocol.Operator.Subtract => RawAst.Operator.create_Minus(),
    DafnyB3Protocol.Operator.Multiply => RawAst.Operator.create_Times(),
    DafnyB3Protocol.Operator.Divide => RawAst.Operator.create_Div(),
    DafnyB3Protocol.Operator.Modulo => RawAst.Operator.create_Mod(),
    DafnyB3Protocol.Operator.RealDivide => RawAst.Operator.create_RealDiv(),
    DafnyB3Protocol.Operator.ToReal => RawAst.Operator.create_ToReal(),
    DafnyB3Protocol.Operator.ToInt => RawAst.Operator.create_ToInt(),
    DafnyB3Protocol.Operator.Not => RawAst.Operator.create_LogicalNot(),
    DafnyB3Protocol.Operator.Negate => RawAst.Operator.create_UnaryMinus(),
    _ => throw new InvalidDataException("Unknown normalized operator")
  };
  private static Expr Bitvector(BitvectorOperation operation) {
    ProtocolValidation.ValidateBitvectorOperation(operation);
    RawAst._IBitvectorOperator kind = operation.Operator switch {
      DafnyB3Protocol.BitvectorOperator.And => RawAst.BitvectorOperator.create_BvAnd(),
      DafnyB3Protocol.BitvectorOperator.Or => RawAst.BitvectorOperator.create_BvOr(),
      DafnyB3Protocol.BitvectorOperator.Xor => RawAst.BitvectorOperator.create_BvXor(),
      DafnyB3Protocol.BitvectorOperator.Not => RawAst.BitvectorOperator.create_BvNot(),
      DafnyB3Protocol.BitvectorOperator.Add => RawAst.BitvectorOperator.create_BvAdd(),
      DafnyB3Protocol.BitvectorOperator.Subtract => RawAst.BitvectorOperator.create_BvSubtract(),
      DafnyB3Protocol.BitvectorOperator.Multiply => RawAst.BitvectorOperator.create_BvMultiply(),
      DafnyB3Protocol.BitvectorOperator.UnsignedDivide => RawAst.BitvectorOperator.create_BvUnsignedDivide(),
      DafnyB3Protocol.BitvectorOperator.UnsignedRemainder => RawAst.BitvectorOperator.create_BvUnsignedRemainder(),
      DafnyB3Protocol.BitvectorOperator.UnsignedLess => RawAst.BitvectorOperator.create_BvUnsignedLess(),
      DafnyB3Protocol.BitvectorOperator.UnsignedLessEqual => RawAst.BitvectorOperator.create_BvUnsignedLessEqual(),
      DafnyB3Protocol.BitvectorOperator.ShiftLeft => RawAst.BitvectorOperator.create_BvShiftLeft(),
      DafnyB3Protocol.BitvectorOperator.LogicalShiftRight => RawAst.BitvectorOperator.create_BvLogicalShiftRight(),
      DafnyB3Protocol.BitvectorOperator.Extract => RawAst.BitvectorOperator.create_BvExtract(),
      DafnyB3Protocol.BitvectorOperator.Concat => RawAst.BitvectorOperator.create_BvConcat(),
      DafnyB3Protocol.BitvectorOperator.IntToBitvector => RawAst.BitvectorOperator.create_IntToBv(),
      DafnyB3Protocol.BitvectorOperator.BitvectorToUnsignedInt => RawAst.BitvectorOperator.create_BvToUnsignedInt(),
      _ => throw new InvalidDataException("Unknown normalized bitvector operator")
    };
    return RawAst.Expr.create_OperatorExpr(RawAst.Operator.create_Bv(kind, operation.Width, operation.Start, operation.End),
      Seq(operation.Arguments.Select(Expression)));
  }
  private static Expr Rational(RationalLiteral literal) {
    var (numerator, denominator) = ProtocolValidation.ParseRationalLiteral(literal);
    return RawAst.Expr.create_RLiteral(numerator, denominator);
  }
  public static RuneString S(string value) => Sequence<Rune>.UnicodeFromString(value);
  private static ISequence<T> Seq<T>(IEnumerable<T> values) => Sequence<T>.FromArray(values.ToArray());
  private static _IOption<T> None<T>() => Option<T>.create_None();
  private static _IOption<T> Some<T>(T value) => Option<T>.create_Some(value);
}
