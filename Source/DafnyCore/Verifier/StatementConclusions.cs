using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// Under --statement-expression-conclusions, an expression in a split position (an assertion, a
/// postcondition, an invariant, a precondition at a call, and so on) has each boolean statement
/// expression "S; E" that sits under a quantifier or a logical connective replaced by "C ==> E",
/// or by "C && E" where it occurs negatively, C being the statement's conclusion
/// (StmtExpr.GetStatementConclusion).  The translator otherwise drops S, and uses C only for a
/// statement expression at the top of a split (TrSplitExpr's StmtExpr case).
///
/// The replacement keeps the value of the expression.  Its well-formedness check, which runs
/// before the expression is used, asserts (or, for an assume statement, assumes) C at every point
/// where S; E is evaluated: under the bound variables' type and allocation antecedents and the
/// range of each enclosing quantifier, under the guards of short-circuit operators and of if-then-
/// else, and with let and match bindings.  The translation of the quantifier, connective or binding
/// has the same antecedents and guards, so wherever the value of S; E counts, C holds, and E,
/// C ==> E and C && E are equal.  Where S; E is not evaluated, the enclosing operator does not
/// depend on its value.  The rewriter does not enter a lambda, whose application is defined beyond
/// its precondition, or a set or map comprehension, whose value is a collection compared
/// extensionally elsewhere; a statement expression there keeps today's translation.
/// </summary>
internal class StatementConclusionSubstituter : Substituter {
  private bool positive;
  // Subexpressions whose polarity is the opposite of their parent's: the operand of a negation,
  // the antecedent of an implication and the range of a universal quantifier.
  private readonly HashSet<Expression> negated = new(ReferenceEqualityComparer.Instance);

  private StatementConclusionSubstituter(bool positive)
    : base(null, new Dictionary<IVariable, Expression>(), new Dictionary<TypeParameter, Type>()) {
    this.positive = positive;
  }

  /// <summary>
  /// Returns "expr" with its boolean statement expressions replaced as above.  "positive" is the
  /// polarity of "expr" itself; it chooses between "C ==> E" and "C && E" and does not affect
  /// soundness, since both equal E wherever C holds.
  /// </summary>
  public static Expression Rewrite(Expression expr, bool positive) {
    return new StatementConclusionSubstituter(positive).Substitute(expr);
  }

  /// <summary>
  /// Whether "expr" has a boolean statement expression that Rewrite would replace.
  /// </summary>
  public static bool Applies(Expression expr) {
    if (expr is LambdaExpr or SetComprehension or MapComprehension) {
      return false;
    }
    if (expr is StmtExpr { E.Type.IsBoolType: true }) {
      return true;
    }
    return expr.SubExpressions.Any(Applies);
  }

  public override Expression Substitute(Expression expr) {
    var flip = negated.Remove(expr);
    var outer = positive;
    if (flip) {
      positive = !positive;
    }
    try {
      switch (expr) {
        case LambdaExpr or SetComprehension or MapComprehension:
          return expr;
        case StmtExpr { E.Type.IsBoolType: true } stmtExpr: {
            var body = Substitute(stmtExpr.E);
            var polarity = positive;
            var conclusion = Substitute(stmtExpr.GetStatementConclusion());
            positive = polarity;
            if (LiteralExpr.IsTrue(conclusion)) {
              return body;
            }
            var op = positive ? BinaryExpr.Opcode.Imp : BinaryExpr.Opcode.And;
            return new BinaryExpr(body.Origin, op, conclusion, body) {
              ResolvedOp = positive ? BinaryExpr.ResolvedOpcode.Imp : BinaryExpr.ResolvedOpcode.And,
              Type = Type.Bool
            };
          }
        case UnaryOpExpr { Op: UnaryOpExpr.Opcode.Not } not:
          negated.Add(not.E);
          break;
        case BinaryExpr { ResolvedOp: BinaryExpr.ResolvedOpcode.Imp } imp:
          negated.Add(imp.E0);
          break;
        case ForallExpr { Range: not null, SplitQuantifier: null } forall:
          negated.Add(forall.Range);
          break;
      }
      return base.Substitute(expr);
    }
    finally {
      positive = outer;
    }
  }
}
