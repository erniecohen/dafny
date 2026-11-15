using System.Diagnostics.Contracts;
using Microsoft.Boogie;
using static Microsoft.Dafny.ResolutionErrors;

namespace Microsoft.Dafny;

public class Abstemious {
  private readonly ErrorReporter reporter;

  public Abstemious(ErrorReporter reporter) {
    this.reporter = reporter;
  }

  public void Check(Function fn) {
    if (fn.Body != null) {
      var abstemious = true;
      if (Attributes.ContainsBool(fn.Attributes, "abstemious", ref abstemious) && abstemious) {
        if (CoCallResolution.GuaranteedCoCtors(fn, reporter.Options.Get(CommonOptionBag.ExtendedNewtypeBases)) == 0) {
          reporter.Error(MessageSource.Resolver, ErrorId.r_abstemious_needs_conconstructor, fn, "the value returned by an abstemious function must come from invoking a co-constructor");
        } else {
          CheckDestructsAreAbstemiousCompliant(fn.Body);
        }
      }
    }
  }

  private Expression StripCoDatatypeViews(Expression expr) {
    expr = Expression.StripParens(expr).Resolved;
    while (expr is ConversionExpr conversion && NewtypeOperationView.IsCoDatatypeIdentityConversion(
             conversion, reporter.Options.Get(CommonOptionBag.ExtendedNewtypeBases))) {
      expr = Expression.StripParens(conversion.E).Resolved;
    }
    return expr;
  }

  private void CheckDestructsAreAbstemiousCompliant(Expression expr) {
    Contract.Assert(expr != null);
    expr = expr.Resolved;
    if (expr is MemberSelectExpr) {
      var e = (MemberSelectExpr)expr;
      if (e.Member.EnclosingClass is CoDatatypeDecl) {
        var ide = StripCoDatatypeViews(e.Obj) as IdentifierExpr;
        if (ide != null && ide.Var is Formal) {
          // cool
        } else {
          reporter.Error(MessageSource.Resolver, ErrorId.r_bad_astemious_destructor, expr, "an abstemious function is allowed to invoke a codatatype destructor only on its parameters");
        }
        return;
      }
    } else if (expr is NestedMatchExpr nestedMatchExpr) {
      if (NewtypeOperationView.IsCoDatatype(nestedMatchExpr.Source.Type, reporter.Options.Get(CommonOptionBag.ExtendedNewtypeBases))) {
        var ide = StripCoDatatypeViews(nestedMatchExpr.Source) as IdentifierExpr;
        if (ide != null && ide.Var is Formal) {
          // cool; fall through to check match branches
        } else {
          reporter.Error(MessageSource.Resolver, ErrorId.r_bad_astemious_nested_match, nestedMatchExpr.Source, "an abstemious function is allowed to codatatype-match only on its parameters");
          return;
        }
      }
    } else if (expr is MatchExpr) {
      var e = (MatchExpr)expr;
      if (NewtypeOperationView.IsCoDatatype(e.Source.Type, reporter.Options.Get(CommonOptionBag.ExtendedNewtypeBases))) {
        var ide = StripCoDatatypeViews(e.Source) as IdentifierExpr;
        if (ide != null && ide.Var is Formal) {
          // cool; fall through to check match branches
        } else {
          reporter.Error(MessageSource.Resolver, ErrorId.r_bad_astemious_match, e.Source, "an abstemious function is allowed to codatatype-match only on its parameters");
          return;
        }
      }
    } else if (expr is BinaryExpr) {
      var e = (BinaryExpr)expr;
      if (e.ResolvedOp == BinaryExpr.ResolvedOpcode.EqCommon || e.ResolvedOp == BinaryExpr.ResolvedOpcode.NeqCommon) {
        if (NewtypeOperationView.IsCoDatatype(e.E0.Type, reporter.Options.Get(CommonOptionBag.ExtendedNewtypeBases))) {
          reporter.Error(MessageSource.Resolver, ErrorId.r_bad_astemious_codatatype_equality, expr, "an abstemious function is not allowed to check codatatype equality");
          return;
        }
      }
    } else if (expr is StmtExpr) {
      var e = (StmtExpr)expr;
      // ignore the statement part
      CheckDestructsAreAbstemiousCompliant(e.E);
      return;
    }
    expr.SubExpressions.ForEach(CheckDestructsAreAbstemiousCompliant);
  }
}