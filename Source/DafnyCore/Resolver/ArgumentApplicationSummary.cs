#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// A checked effect summary for ghost helpers. Arrow arguments may be applied or consumed by another
/// summarized helper, but cannot escape as ordinary data. The least fixpoint never admits a mutually
/// recursive group on its own authority. Direct self recursion is allowed only with the same arrow
/// parameters; its termination is still verified independently, without an order supplied by the helper.
/// </summary>
internal static class ArgumentApplicationSummary {
  public static HashSet<MemberDecl> Compute(Program program) {
    var result = new HashSet<MemberDecl>();
    if (program.Options.Get(DeveloperOptionBag.Bootstrapping)) {
      // This internal mode declares its input part of the Dafny implementation. While building
      // Orders itself there is no bundled-library URI: retain its three existing primitive effects.
      // All user-defined helpers, including the new constructions, still undergo the shape check.
      foreach (var member in program.Modules().Where(m => m.FullDafnyName == DecreasesOrders.WfoModule)
                 .SelectMany(m => m.TopLevelDecls).OfType<DefaultClassDecl>().SelectMany(c => c.Members)) {
        if (member.Name is DecreasesOrders.OfName or DecreasesOrders.WellFoundedName or DecreasesOrders.StopsName) {
          result.Add(member);
        }
      }
    }
    var candidates = program.Modules().SelectMany(m => m.TopLevelDecls)
      .OfType<TopLevelDeclWithMembers>().SelectMany(c => c.Members)
      .Where(m => m is ICallable && m.IsGhost && m.IsStatic && m.OverriddenMember == null &&
        Attributes.Contains(m.Attributes, ModularTermination.ClosedAttributeName) &&
        !Attributes.Contains(m.Attributes, "axiom") && !Attributes.Contains(m.Attributes, "extern") &&
        !Attributes.Contains(m.Attributes, "verify") && !Attributes.Contains(m.Attributes, "termination") &&
        (m is Function { Body: not null } || m is Lemma { Body: not null }))
      .ToList();
    bool changed;
    do {
      changed = false;
      foreach (var member in candidates) {
        // A bodyless Closed declaration promises unconditional closure. Refinement may not
        // weaken that promise to an argument-dependent effect which its old callers did not forward.
        if (!result.Contains(member) &&
            (member.RefinementBase == null || result.Contains(member.RefinementBase)) &&
            new Checker(member, result).Check()) {
          result.Add(member);
          changed = true;
        }
      }
    } while (changed);
    return result;
  }

  private sealed class Checker(MemberDecl owner, HashSet<MemberDecl> known) {
    private readonly ICallable callable = (ICallable)owner;
    private readonly Dictionary<IVariable, Expression> aliases = new();
    private readonly HashSet<IVariable> binders = [];

    private bool Summary(MemberDecl member) => DecreasesOrders.IsIntrinsicArgumentApplication(member) || known.Contains(member);
    private static bool Arrow(Expression e) => e.Type.NormalizeExpand().IsArrowType;

    public bool Check() {
      // An order cannot supply the termination argument for the helper whose effects it summarizes.
      if (callable.Decreases.Expressions.Any(e => e is DecreasesByExpr)) {
        return false;
      }
      if (!callable.Decreases.Expressions.All(e => Expr(e)) ||
          !callable.Ins.Where(f => f.DefaultValue != null).All(f => Expr(f.DefaultValue!))) {
        return false;
      }
      return owner switch {
        Function f => f.Req.All(r => Expr(r.E)) && f.Ens.All(r => Expr(r.E)) &&
          f.Reads.Expressions.All(r => Expr(r.E)) && Expr(f.Body!),
        Lemma l => l.Req.All(r => Expr(r.E)) && l.Ens.All(r => Expr(r.E)) &&
          l.Reads.Expressions.All(r => Expr(r.E)) && l.Mod.Expressions.All(r => Expr(r.E)) && Stmt(l.Body!),
        _ => false
      };
    }

    private bool Call(MemberDecl member, IReadOnlyList<Expression> args) {
      var callee = member as ICallable;
      if (member == owner) {
        // No cyclic inference: only direct, parameter-preserving self calls get the current shape.
        return args.Count == callable.Ins.Count && args.Select((a, i) =>
          !callable.Ins[i].Type.NormalizeExpand().IsArrowType ||
          a.Resolved is IdentifierExpr id && id.Var == callable.Ins[i]).All(b => b) &&
          args.Select(a => Expr(a, Arrow(a))).All(b => b);
      }
      var summary = Summary(member);
      if (!summary && (!ModularTermination.HasClosedContract(member) || args.Any(Arrow))) {
        return false;
      }
      return args.Select((a, i) => Expr(a, summary && callee != null && i < callee.Ins.Count &&
        callee.Ins[i].Type.NormalizeExpand().IsArrowType)).All(b => b);
    }

    private bool Expr(Expression expression, bool consumed = false) {
      var e = expression.Resolved;
      switch (e) {
        case IdentifierExpr id when Arrow(id):
          return consumed && (callable.Ins.Contains(id.Var) || binders.Contains(id.Var) || aliases.ContainsKey(id.Var));
        case LambdaExpr lambda:
          // A closure is permitted only as an immediate argument of a summarized consumer or a local
          // alias used in those same positions. In particular, returning or storing it is not permitted.
          return consumed && lambda.SubExpressions.All(x => Expr(x));
        case FunctionCallExpr call when ModularTermination.IsArrowMember(call.Function):
          return Expr(call.Receiver, true) && call.Args.All(a => Expr(a));
        case FunctionCallExpr call when Arrow(call) &&
          (!consumed || !DecreasesOrders.IsLibraryMultisetRel(call.Function)):
          return false;
        case FunctionCallExpr call:
          return Call(call.Function, call.Args) && Expr(call.Receiver);
        case ApplyExpr apply:
          return !Arrow(apply) && Expr(apply.Function, true) && apply.Args.All(a => Expr(a));
        case MemberSelectExpr select when select.Member is Function:
          // A named, independently closed function is a permissible argument, not an unknown closure.
          return ModularTermination.HasClosedContract(select.Member) && Expr(select.Obj);
        case QuantifierExpr quantifier:
          foreach (var b in quantifier.BoundVars) { binders.Add(b); }
          return quantifier.SubExpressions.All(x => Expr(x));
        case LetExpr let when let.Exact:
          for (var i = 0; i < let.RHSs.Count; i++) {
            var vars = let.LHSs[i].Vars.ToList();
            var rhs = let.RHSs[i];
            if (Arrow(rhs)) {
              if (vars.Count != 1 || !Expr(rhs, true) || !aliases.TryAdd(vars[0], rhs)) { return false; }
            } else if (!Expr(rhs)) { return false; }
          }
          return Expr(let.Body, consumed);
        case StmtExpr statement:
          return Stmt(statement.S) && Expr(statement.E, consumed);
        default:
          return !Arrow(e) && e.SubExpressions.All(x => Expr(x));
      }
    }

    private bool Stmt(Statement statement) {
      switch (statement) {
        case ForallStmt forall:
          foreach (var b in forall.BoundVars) { binders.Add(b); }
          break;
        case SingleAssignStmt { Lhs: IdentifierExpr id, Rhs: ExprRhs rhs } when Arrow(rhs.Expr):
          return id.Var is LocalVariable && Expr(rhs.Expr, true) && aliases.TryAdd(id.Var, rhs.Expr);
        case CallStmt call:
          return Call(call.Method, call.Args) && Expr(call.Receiver) && call.Lhs.All(e => Expr(e));
      }
      return statement.SubExpressions.All(e => Expr(e)) && statement.SubStatements.All(Stmt);
    }
  }
}
