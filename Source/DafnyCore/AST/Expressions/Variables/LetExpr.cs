#nullable enable

using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Microsoft.Dafny;

public class LetExpr : Expression, IAttributeBearingDeclaration, IBoundVarsBearingExpression, ICloneable<LetExpr>, ICanFormat {
  public List<CasePattern<BoundVar>> LHSs;
  public List<Expression> RHSs;
  public Expression Body;
  public bool Exact;  // Exact==true means a regular let expression; Exact==false means an assign-such-that expression

  public Attributes? Attributes { get; set; }

  string IAttributeBearingDeclaration.WhatKind => "let expression";

  // null for Exact=true and for when expression is in a ghost context
  [FilledInDuringResolution] public List<BoundedPool>? Constraint_Bounds;
  // invariant Constraint_Bounds == null || Constraint_Bounds.Count == BoundVars.Count;

  /// <summary>
  /// Non-null for an expression that was written "var y1, ..., yk := L(args); e", where L is a lemma (see
  /// --lemma-bindings).  Resolution rewrites such a let expression in place.  The rewritten form is
  ///
  ///     var t1, ..., tn := receiver-and-arguments;                 // omitted if there is nothing to name
  ///     var y1, ..., yk := (var z1, ..., zk := L(t1, ..., tn); R1), R2, ..., Rk;
  ///     e
  ///
  /// where the ti are fresh ghost variables, the parenthesized expression is a StmtExpr whose statement declares
  /// fresh ghost locals zi and calls L, and each Ri is a LemmaResultExpr for out-parameter i of L at the ti.
  /// "this" node is the outermost of these let expressions.  The record keeps what was written, for printing and
  /// for cloning the unresolved form.
  /// </summary>
  [FilledInDuringResolution] public LemmaBindingSyntax? LemmaBinding;

  public record LemmaBindingSyntax(List<CasePattern<BoundVar>> Lhss, ApplySuffix Rhs, bool HasArgumentLet);

  /// <summary>
  /// For a let expression with a non-null LemmaBinding, returns the let expression that binds the variables the
  /// user wrote (which is either "this" or the body of "this").
  /// </summary>
  public LetExpr LemmaBindingResultLet {
    get {
      Contract.Requires(LemmaBinding != null);
      return LemmaBinding!.HasArgumentLet ? (LetExpr)Body : this;
    }
  }

  // filled in during translation, lazily; to be accessed only via Translation.LetDesugaring; always null when Exact==true
  private Expression? translationDesugaring;

  private BoogieGenerator? lastBoogieGeneratorUsed; // avoid clashing desugaring between translators

  public void SetTranslationDesugaring(BoogieGenerator trans, Expression expr) {
    if (ModuleNamingScope.Current is { } scope) {
      scope.SetValue(this, "let-desugaring", expr);
      return;
    }
    lastBoogieGeneratorUsed = trans;
    translationDesugaring = expr;
  }

  public Expression? GetTranslationDesugaring(BoogieGenerator trans) {
    if (ModuleNamingScope.Current is { } scope) {
      return scope.FindValue<Expression>(this, "let-desugaring");
    }
    return lastBoogieGeneratorUsed == trans ? translationDesugaring : null;
  }

  public LetExpr Clone(Cloner cloner) {
    return new LetExpr(cloner, this);
  }

  public LetExpr(Cloner cloner, LetExpr original) : base(cloner, original) {
    if (original.LemmaBinding != null && !cloner.CloneResolvedFields) {
      // clone what was written, not what resolution made of it
      LHSs = original.LemmaBinding.Lhss.ConvertAll(cloner.CloneCasePattern);
      RHSs = [cloner.CloneExpr(original.LemmaBinding.Rhs)];
      Body = cloner.CloneExpr(original.LemmaBindingResultLet.Body);
    } else {
      LHSs = original.LHSs.ConvertAll(cloner.CloneCasePattern);
      RHSs = original.RHSs.ConvertAll(cloner.CloneExpr);
      Body = cloner.CloneExpr(original.Body);
      LemmaBinding = original.LemmaBinding;
    }
    Exact = original.Exact;
    Attributes = cloner.CloneAttributes(original.Attributes);
    if (cloner.CloneResolvedFields) {
      Constraint_Bounds = original.Constraint_Bounds;
    }
  }

  [SyntaxConstructor]
  public LetExpr(IOrigin origin, List<CasePattern<BoundVar>> lhss, List<Expression> rhss, Expression body, bool exact, Attributes? attributes = null)
    : base(origin) {
    LHSs = lhss;
    RHSs = rhss;
    Body = body;
    Exact = exact;
    Attributes = attributes;
  }

  public static LetExpr Havoc(IOrigin tok, Type? type = null) {
    type ??= new InferredTypeProxy();
    var boundVar = new BoundVar(tok, "x", type);
    var casePatterns = new List<CasePattern<BoundVar>>() { new(tok, boundVar) };
    return new LetExpr(tok, casePatterns, [CreateBoolLiteral(tok, true)],
      new IdentifierExpr(tok, boundVar), false) {
      Type = type
    };
  }

  public override IEnumerable<Expression> SubExpressions {
    get {
      foreach (var e in Attributes.SubExpressions(Attributes)) {
        yield return e;
      }
      foreach (var rhs in RHSs) {
        yield return rhs;
      }
      yield return Body;
    }
  }

  public override IEnumerable<Expression> TerminalExpressions {
    get {
      // The terminal expressions of a let expression are usually the terminal expressions of
      // the let's body. However, if anyone of those terminal expressions is a simple bound
      // variable of an exact let expression, then that terminal expression is replaced by
      // the terminal expressions of the corresponding RHS.
      // For example, the terminal expressions of
      //     var x := E;
      //     assert P(x);
      //     x
      // are the terminal expressions of E.
      Contract.Assert(!Exact || LHSs.Count == RHSs.Count);
      var rhsUsed = new bool[LHSs.Count];
      foreach (var e in Body.TerminalExpressions) {
        if (Exact) {
          for (var i = 0; i < LHSs.Count; i++) {
            if (LHSs[i].Var != null && IdentifierExpr.Is(e, LHSs[i].Var)) {
              if (!rhsUsed[i]) {
                foreach (var ee in RHSs[i].TerminalExpressions) {
                  yield return ee;
                }
                rhsUsed[i] = true;
              }
              goto Next;
            }
          }
        }
        yield return e;
      Next: { }
      }
    }
  }

  public override IEnumerable<Type> ComponentTypes => BoundVars.Select(bv => bv.Type);

  public IEnumerable<BoundVar> BoundVars {
    get {
      foreach (var lhs in LHSs) {
        foreach (var bv in lhs.Vars) {
          yield return bv;
        }
      }
    }
  }

  public IEnumerable<BoundVar> AllBoundVars => BoundVars;

  public override IEnumerable<INode> Children =>
    Attributes.AsEnumerable().
      Concat<Node>(LHSs)
    .Concat(base.Children);

  public bool SetIndent(int indentBefore, TokenNewIndentCollector formatter) {
    return formatter.SetIndentVarDeclStmt(indentBefore, OwnedTokens, false, true);
  }
}
