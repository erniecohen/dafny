using System.Collections.Generic;

namespace Microsoft.Dafny;

/// <summary>
/// A decreases-clause component "E by Order" (--decreases-orders).  Order is a closed ghost
/// expression of type Std.Termination.Orders.Wfo&lt;T&gt;, where T is the type of E.  The component is
/// compared by Order.gt(old E, E) instead of the built-in order.  After resolution, ResolvedExpression is E,
/// so everything that does not look for the order treats the component as E.
/// </summary>
public class DecreasesByExpr : ConcreteSyntaxExpression, ICloneable<DecreasesByExpr> {
  public Expression E;
  public Expression Order;

  public DecreasesByExpr(IOrigin origin, Expression e, Expression order)
    : base(origin) {
    E = e;
    Order = order;
  }

  protected DecreasesByExpr(Cloner cloner, DecreasesByExpr original) : base(cloner, original) {
    E = cloner.CloneExpr(original.E);
    Order = cloner.CloneExpr(original.Order);
  }

  public override IEnumerable<Expression> SubExpressions {
    get {
      yield return ResolvedExpression ?? E;
      yield return Order;
    }
  }

  public override IEnumerable<Expression> PreResolveSubExpressions {
    get {
      yield return E;
      yield return Order;
    }
  }

  public DecreasesByExpr Clone(Cloner cloner) {
    return new DecreasesByExpr(cloner, this);
  }

  /// <summary>
  /// "e", given the order of "original" if "e" has lost it (for example, by a rewrite to the value on loop entry).
  /// </summary>
  public static Expression WithOrderOf(Expression original, Expression e) {
    if (OrderOf(original) is not { } order || OrderOf(e) != null) {
      return e;
    }
    return new DecreasesByExpr(e.Origin, e, order) { ResolvedExpression = e, Type = e.Type };
  }

  /// <summary>
  /// The order of a decreases component, looking through parentheses and "old", or null.
  /// </summary>
  public static Expression OrderOf(Expression e) {
    while (true) {
      switch (e) {
        case DecreasesByExpr d: return d.Order;
        case ParensExpression p: e = p.E; break;
        case OldExpr o: e = o.Expr; break;
        default: return null;
      }
    }
  }
}
