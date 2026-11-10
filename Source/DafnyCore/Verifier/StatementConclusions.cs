using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// Under --statement-expression-conclusions, the condition of an assert statement has each boolean
/// statement expression "S; E" inside it replaced by "C ==> E", or by "C && E" where it occurs
/// negatively, C being the statement's conclusion (StmtExpr.GetStatementConclusion).  The
/// translator otherwise drops S, and uses C only for a statement expression at the top of a split
/// (TrSplitExpr's StmtExpr case).  The caller (TrAssertCondition) applies it only when the assert
/// statement is translated in the current heap, right after TrAssertStmt has checked the
/// well-formedness of the same condition with the same translator.
///
/// The replacement keeps the value of the condition.  That well-formedness check asserts (or, for
/// an assume statement, assumes) C at every point where S; E is evaluated, and the rewriter only
/// descends through constructs whose translation has the same guards as their well-formedness
/// check: logical connectives, if-then-else, exact let, match, and quantifiers over the current
/// heap.  A quantifier's translation may leave out the allocation antecedent of a bound variable
/// whose range implies allocation (freeOfAlloc), while the check assumes allocation; that
/// implication holds in the current heap only.  So the rewriter does not enter old, unchanged or
/// fresh, which evaluate in another heap.  It does not enter a lambda (whose application is defined
/// beyond its precondition), a set or map comprehension (compared extensionally elsewhere), a
/// let-such-that or a binding-guard if-then-else (whose check and translation pick the witness
/// differently).  There a statement expression keeps today's translation.  Wherever the value of
/// S; E counts, C holds, and E, C ==> E and C && E are equal; where S; E is not evaluated, the
/// enclosing operator does not depend on its value.  docs/dev/statement-expression-conclusions.md
/// has the full argument.
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
    if (IsOpaque(expr)) {
      return false;
    }
    // A statement whose conclusion is the literal true (a call, reveal, hide or forall statement)
    // adds nothing, and does not by itself call for a rewrite.
    if (expr is StmtExpr { E.Type.IsBoolType: true } stmtExpr && !LiteralExpr.IsTrue(stmtExpr.GetStatementConclusion())) {
      return true;
    }
    return expr.SubExpressions.Any(Applies);
  }

  /// <summary>
  /// The expressions the rewriter does not enter (see above).
  /// </summary>
  private static bool IsOpaque(Expression expr) {
    return expr is LambdaExpr or SetComprehension or MapComprehension
      or OldExpr or UnchangedExpr or FreshExpr
      or LetExpr { Exact: false } or ITEExpr { IsBindingGuard: true };
  }

  public override Expression Substitute(Expression expr) {
    var flip = negated.Remove(expr);
    var outer = positive;
    if (flip) {
      positive = !positive;
    }
    try {
      switch (expr) {
        case var _ when IsOpaque(expr):
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
