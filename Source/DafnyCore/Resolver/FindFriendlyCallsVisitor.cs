using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Microsoft.Dafny;

public enum CallingPosition { Positive, Negative, Neither }

class FindFriendlyCallsVisitor : ResolverTopDownVisitor<CallingPosition> {
  public readonly bool IsCoContext;
  public readonly bool ContinuityIsImportant;
  protected bool InsideOrdinalUnsafeQuantifier;
  public FindFriendlyCallsVisitor(ErrorReporter reporter, bool co, bool continuityIsImportant)
    : base(reporter) {
    Contract.Requires(reporter != null);
    this.IsCoContext = co;
    this.ContinuityIsImportant = continuityIsImportant;
  }

  public void KNatMismatchError(IOrigin tok, string contextName, ExtremePredicate.KType contextK, ExtremePredicate.KType calleeK) {
    var hint = contextK == ExtremePredicate.KType.Unspecified ? string.Format(" (perhaps try declaring '{0}' as '{0}[nat]')", contextName) : "";
    reporter.Error(MessageSource.Resolver, tok,
      "this call does not type check, because the context uses a _k parameter of type {0} whereas the callee uses a _k parameter of type {1}{2}",
      contextK == ExtremePredicate.KType.Nat ? "nat" : "ORDINAL",
      calleeK == ExtremePredicate.KType.Nat ? "nat" : "ORDINAL",
      hint);
  }

  static CallingPosition Invert(CallingPosition cp) {
    switch (cp) {
      case CallingPosition.Positive: return CallingPosition.Negative;
      case CallingPosition.Negative: return CallingPosition.Positive;
      default: return CallingPosition.Neither;
    }
  }

  protected override bool VisitOneExpr(Expression expr, ref CallingPosition cp) {
    if (expr is UnaryOpExpr) {
      var e = (UnaryOpExpr)expr;
      if (e.Op == UnaryOpExpr.Opcode.Not) {
        // for the sub-parts, use Invert(cp)
        cp = Invert(cp);
        return true;
      }
    } else if (expr is BinaryExpr) {
      var e = (BinaryExpr)expr;
      switch (e.ResolvedOp) {
        case BinaryExpr.ResolvedOpcode.And:
        case BinaryExpr.ResolvedOpcode.Or:
          return true;  // do the sub-parts with the same "cp"
        case BinaryExpr.ResolvedOpcode.Imp:
          Visit(e.E0, Invert(cp));
          Visit(e.E1, cp);
          return false;  // don't recurse (again) on the sub-parts
        default:
          break;
      }
    } else if (expr is NestedMatchExpr) {
      var e = (NestedMatchExpr)expr;
      Visit(e.Source, CallingPosition.Neither);
      var theCp = cp;
      e.Cases.ForEach(kase => Visit((Expression)kase.Body, theCp));
      return false;
    } else if (expr is MatchExpr) {
      var e = (MatchExpr)expr;
      Visit(e.Source, CallingPosition.Neither);
      var theCp = cp;
      e.Cases.ForEach(kase => Visit(kase.Body, theCp));
      return false;
    } else if (expr is ITEExpr) {
      var e = (ITEExpr)expr;
      Visit(e.Test, CallingPosition.Neither);
      Visit(e.Thn, cp);
      Visit(e.Els, cp);
      return false;
    } else if (expr is LetExpr) {
      var e = (LetExpr)expr;
      foreach (var rhs in e.RHSs) {
        Visit(rhs, CallingPosition.Neither);
      }
      var cpBody = cp;
      if (!e.Exact) {
        // a let-such-that expression introduces an existential that may depend on the _k in a least/greatest predicate, so we disallow recursive calls in the body of the let-such-that
        if (IsCoContext && cp == CallingPosition.Positive) {
          cpBody = CallingPosition.Neither;
        } else if (!IsCoContext && cp == CallingPosition.Negative) {
          cpBody = CallingPosition.Neither;
        }
      }
      Visit(e.Body, cpBody);
      return false;
    } else if (expr is QuantifierExpr) {
      var e = (QuantifierExpr)expr;
      Contract.Assert(e.SplitQuantifier == null); // No split quantifiers during resolution
      var cpos = IsCoContext ? cp : Invert(cp);
      var previousOrdinalUnsafeQuantifier = InsideOrdinalUnsafeQuantifier;
      if ((cpos == CallingPosition.Positive && e is ExistsExpr) || (cpos == CallingPosition.Negative && e is ForallExpr)) {
        for (var i = 0; i < e.BoundVars.Count; i++) {
          var bound = e.Bounds?[i];
          var finite = bound != null && (bound.Virtues & BoundedPool.PoolVirtues.Finite) != 0;
          if (ContinuityIsImportant ? !finite :
              !(finite && bound is not DatatypeInclusionBoundedPool) &&
              !HasSetSizedDomain(e.BoundVars[i].Type, new HashSet<DatatypeDecl>())) {
            // Natural indexing requires finite branching. Ordinal indexing permits set-sized
            // branching, but not quantification over all ordinals (even nested inside a type).
            cp = CallingPosition.Neither;
            InsideOrdinalUnsafeQuantifier |= !ContinuityIsImportant;
            break;
          }
        }
      }
      Visit(e.LogicalBody(), cp);
      InsideOrdinalUnsafeQuantifier = previousOrdinalUnsafeQuantifier;
      return false;
    } else if (expr is StmtExpr) {
      var e = (StmtExpr)expr;
      Visit(e.E, cp);
      Visit(e.S, CallingPosition.Neither);
      return false;
    } else if (expr is ConcreteSyntaxExpression) {
      // do the sub-parts with the same "cp"
      return true;
    }
    // do the sub-parts with cp := Neither
    cp = CallingPosition.Neither;
    return true;
  }

  // A sufficient, deliberately conservative test. Unknown and hidden types might contain
  // ordinals. A datatype rank bound is not a finite domain: equally ranked constructors
  // may contain arbitrarily large ordinals, so callers must not rely on that pool here.
  private static bool HasSetSizedDomain(Type type, HashSet<DatatypeDecl> visited) {
    type = type.NormalizeToAncestorType();
    if (type.IsRefType) {
      return true;
    }
    if (type is BasicType) {
      return !type.IsBigOrdinalType;
    }
    // Check actual arguments before the declaration cycle guard, including polymorphic recursion.
    if (!type.TypeArgs.All(argument => HasSetSizedDomain(argument, visited))) {
      return false;
    }
    if (type is CollectionType or MapType or ArrowType) {
      return true;
    }
    if (type is UserDefinedType { ResolvedClass: DatatypeDecl datatype }) {
      if (!visited.Add(datatype)) {
        return true;
      }
      var substitution = TypeParameter.SubstitutionMap(datatype.TypeArgs, type.TypeArgs);
      return datatype.Ctors.All(ctor => ctor.Formals.All(formal =>
        HasSetSizedDomain(formal.Type.Subst(substitution), visited)));
    }
    return false;
  }

}