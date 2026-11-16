using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny.Triggers;

/// <summary>
/// Under --detect-witness-matching-loops, matching-loop detection also follows the witnesses of nested
/// existentials (https://github.com/erniecohen/dafny/issues/202).
///
/// When a quantifier is instantiated, every quantifier nested in its body that the solver skolemizes yields
/// a fresh term for each of its bound variables: in
///   forall q | q in s :: exists p :: p in s &amp;&amp; q &lt; p
/// each instance at q produces a witness sk(q) with sk(q) in s, which matches the trigger q in s again.
/// Such a bound variable is a witness variable of the outer quantifier.  Loop detection otherwise treats a
/// match that differs from a trigger term only by one variable for another, here p for q, as harmless,
/// which is right for a variable that is bound by the outer quantifier or free in it, and wrong for a
/// witness.
///
/// A nested quantifier is skolemized when the outer quantifier is instantiated if it is an existential in
/// positive position, or a universal in negative position, relative to the outer quantifier's body taken
/// in the polarity in which the outer quantifier is instantiated (positive for a universal, negative for an
/// existential).  Where the polarity is mixed or unknown (an equivalence, an if-then-else condition, an
/// argument of a function), both are assumed, so every nested quantifier there is counted: this errs
/// towards reporting a loop.
/// </summary>
internal static class WitnessVariables {

  internal static ISet<IVariable> Of(ComprehensionExpr comprehension, DafnyOptions options) {
    if (!options.Get(CommonOptionBag.DetectWitnessMatchingLoops) || comprehension is not QuantifierExpr quantifier) {
      return null;
    }
    var witnesses = new HashSet<IVariable>();
    var polarity = quantifier is ForallExpr ? 1 : -1;
    // forall x | R :: T is R ==> T; exists x | R :: T is R && T.
    if (quantifier.Range != null) {
      Collect(quantifier.Range, quantifier is ForallExpr ? -polarity : polarity, witnesses);
    }
    Collect(quantifier.Term, polarity, witnesses);
    return witnesses;
  }

  /// <summary>
  /// polarity: 1 positive, -1 negative, 0 both.
  /// </summary>
  private static void Collect(Expression expr, int polarity, HashSet<IVariable> witnesses) {
    expr = expr.Resolved;
    switch (expr) {
      case UnaryOpExpr { ResolvedOp: UnaryOpExpr.ResolvedOpcode.BoolNot } not:
        Collect(not.E, -polarity, witnesses);
        return;
      case BinaryExpr binary:
        switch (binary.ResolvedOp) {
          case BinaryExpr.ResolvedOpcode.And:
          case BinaryExpr.ResolvedOpcode.Or:
            Collect(binary.E0, polarity, witnesses);
            Collect(binary.E1, polarity, witnesses);
            return;
          case BinaryExpr.ResolvedOpcode.Imp:
            Collect(binary.E0, -polarity, witnesses);
            Collect(binary.E1, polarity, witnesses);
            return;
        }
        break;
      case ITEExpr ite:
        Collect(ite.Test, 0, witnesses);
        Collect(ite.Thn, polarity, witnesses);
        Collect(ite.Els, polarity, witnesses);
        return;
      case QuantifierExpr nested:
        if (nested.SplitQuantifier != null) {
          foreach (var part in nested.SplitQuantifier) {
            Collect(part, polarity, witnesses);
          }
          return;
        }
        var skolemized = nested is ExistsExpr ? polarity != -1 : polarity != 1;
        if (skolemized) {
          witnesses.UnionWith(nested.BoundVars);
        }
        if (nested.Range != null) {
          Collect(nested.Range, nested is ForallExpr ? -polarity : polarity, witnesses);
        }
        Collect(nested.Term, polarity, witnesses);
        return;
    }
    foreach (var sub in expr.SubExpressions) {
      Collect(sub, 0, witnesses);
    }
  }
}
