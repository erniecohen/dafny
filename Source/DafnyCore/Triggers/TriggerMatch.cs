using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny.Triggers;

internal struct TriggerMatch {
  internal Expression Expr;
  internal Expression OriginalExpr;
  internal Dictionary<IVariable, Expression> Bindings;

  internal static bool Eq(TriggerMatch t1, TriggerMatch t2) {
    return ExprExtensions.ExpressionEq(t1.Expr, t2.Expr);
  }

  /// <summary>
  ///  This method checks whether this match could actually cause a loop, given a set of terms participating in a trigger;
  ///  to compute an answer, we match the Expr of this match against the Exprs of each of these term, allowing for harmless
  ///  variations. If any of these tests does match, this term likely won't cause a loop.
  ///  The boundVars list is useful to determine that forall x :: P(x) == P(y+z) does not loop.
  ///  witnessVars, null unless --detect-witness-matching-loops is on, are the variables of nested quantifiers
  ///  that are skolemized when the quantifier is instantiated (see WitnessVariables): a match in which one of
  ///  them takes the place of a bound variable, as p in "p in s" for the trigger "q in s", does loop.
  /// </summary>
  internal bool CouldCauseLoops(List<TriggerTerm> terms, ISet<BoundVar> boundVars, DafnyOptions options, ISet<IVariable> witnessVars = null) {
    var expr = Expr;
    return !terms.Any(term => term.Expr.ExpressionEqModuloExpressionsNotInvolvingBoundVariables(expr, boundVars, options, witnessVars));
  }
}