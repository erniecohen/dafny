#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Microsoft.Dafny;

/// <summary>
/// Support for --modular-termination (https://github.com/erniecohen/dafny/issues/211), milestone M1.
///
/// A method or function may declare a global bound, "decreases {:global} E".  The parser keeps E as
/// the arguments of the clause's {:global} attribute, apart from the ordinary decreases list, and does
/// so only under the option.  An override that declares no global bound inherits its parent's.
///
/// A call needs the smaller-bound rule when the code it reaches is not visible to any one module's
/// call graph: a dynamically dispatched call to a bodyless member of an open trait (see
/// <see cref="OpenTraits"/>), or a call through a function value whose code is not known.  The
/// verifier checks the rule for the first kind only: the callee's global bound must be below the
/// caller's, captured on entry, where a missing bound is above every other.  Calls through function
/// values are only reported.
/// </summary>
public static class ModularTermination {
  public const string GlobalAttributeName = "global";

  public static bool IsGlobalClause(Attributes a) {
    return a.Name == GlobalAttributeName && a.Args is { Count: > 0 } && a is not UserSuppliedAtAttribute;
  }

  /// <summary>
  /// The global decreases clauses among the given attributes, last-written first.
  /// </summary>
  public static IEnumerable<Attributes> GlobalClauses(Attributes? attrs) {
    for (var a = attrs; a != null; a = a.Prev) {
      if (IsGlobalClause(a)) {
        yield return a;
      }
    }
  }

  /// <summary>
  /// The global bound that the callable declares itself, or null.
  /// </summary>
  public static List<Expression>? DeclaredGlobalBound(ICallable callable) {
    if (callable is not (MethodOrConstructor or Function)) {
      return null;
    }
    return GlobalClauses(callable.Decreases.Attributes).LastOrDefault()?.Args;
  }

  /// <summary>
  /// Whether the callable has a global bound, declared or inherited.
  /// </summary>
  public static bool HasGlobalBound(ICallable callable) {
    for (ICallable? c = callable; c != null; c = (c as MemberDecl)?.OverriddenMember as ICallable) {
      if (DeclaredGlobalBound(c) != null) {
        return true;
      }
    }
    return false;
  }

  /// <summary>
  /// The traits whose bodyless members may be implemented in a module that the trait's own module,
  /// and so its call graph, does not see: a trait with {:termination false}, a trait that a type in
  /// another module extends (which @AssumeCrossModuleTermination admits), and every ancestor of
  /// such a trait.
  /// </summary>
  public static HashSet<TraitDecl> OpenTraits(Program program) {
    var seeds = new List<TraitDecl>();
    foreach (var module in program.Modules()) {
      foreach (var decl in module.TopLevelDecls) {
        if (decl is TraitDecl trait && !trait.IsObjectTrait) {
          bool termination = true;
          if (Attributes.ContainsBool(trait.Attributes, "termination", ref termination) && !termination) {
            seeds.Add(trait);
          }
        }
        if (decl is TopLevelDeclWithMembers cl) {
          seeds.AddRange(cl.ParentTraitHeads.Where(parent =>
            !parent.IsObjectTrait && parent.EnclosingModuleDefinition != cl.EnclosingModuleDefinition));
        }
      }
    }
    var open = new HashSet<TraitDecl>();
    var work = new Stack<TraitDecl>(seeds);
    while (work.Count != 0) {
      var trait = work.Pop();
      if (open.Add(trait)) {
        foreach (var parent in trait.ParentTraitHeads.Where(parent => !parent.IsObjectTrait)) {
          work.Push(parent);
        }
      }
    }
    return open;
  }

  /// <summary>
  /// Whether a call to "callee" is dispatched to an implementation that may lie in a module that no
  /// call graph sees: "callee" is a bodyless instance member of an open trait.
  /// </summary>
  public static bool IsOpenDispatch(MemberDecl callee, HashSet<TraitDecl> openTraits) {
    if (callee.IsStatic || callee.EnclosingClass is not TraitDecl trait || !openTraits.Contains(trait)) {
      return false;
    }
    return callee switch {
      Function f => f.Body == null,
      MethodOrConstructor m => m.Body == null,
      _ => false
    };
  }

  /// <summary>
  /// Whether "member" overrides, directly or not, a member that calls dispatch across modules: such a member
  /// inherits the global bound that its dispatched calls need.
  /// </summary>
  public static bool ImplementsOpenDispatch(MemberDecl member, HashSet<TraitDecl> openTraits) {
    for (var m = member.OverriddenMember; m != null; m = m.OverriddenMember) {
      if (IsOpenDispatch(m, openTraits)) {
        return true;
      }
    }
    return false;
  }
}

/// <summary>
/// Which declarations may, when they run, make a call dispatched to implementations that other modules may
/// provide (see <see cref="ModularTermination.IsOpenDispatch"/>), either themselves or through calls to code
/// that is known statically.  Such a declaration is said to reach open dispatch.
///
/// A call to a known declaration that reaches open dispatch is checked by the same-bound rule: the callee runs
/// under its caller's bound, so its own global bound, at the call's arguments, must not exceed the caller's
/// (a missing bound is above every other).  A call to a declaration that does not reach open dispatch needs no
/// check: no call below it is dispatched across modules, and the existing termination checks order all of them.
///
/// The nodes are the methods, functions and constants with initializers of every class, trait and datatype, and
/// the iterators.  The edges are the calls in their bodies, specifications, global bounds and the default values
/// of their parameters (evaluated as part of a call that omits the argument); a reference to a
/// function or method as a value; a reference to a constant; from a call of an iterator's MoveNext, to the iterator;
/// and from a bodyless member of a trait that is not open to each member that overrides it.  A call through a
/// function value is not an edge: such calls are not yet checked.
/// </summary>
public class ModularTerminationAnalysis {
  private static readonly ConditionalWeakTable<Program, ModularTerminationAnalysis> Cache = new();

  public readonly HashSet<TraitDecl> OpenTraits;
  private readonly HashSet<Declaration> reaching = [];

  /// <summary>
  /// The analysis of the program, computed once, the first time it is asked for.  Use it only once the
  /// program is fully resolved.
  /// </summary>
  public static ModularTerminationAnalysis Of(Program program) {
    return Cache.GetValue(program, p => new ModularTerminationAnalysis(p));
  }

  public ModularTerminationAnalysis(Program program) {
    OpenTraits = ModularTermination.OpenTraits(program);
    var edges = new Dictionary<Declaration, HashSet<Declaration>>();
    var callers = new Dictionary<Declaration, List<Declaration>>();
    var work = new Stack<Declaration>();
    void AddNode(Declaration node, System.Action<EdgeCollector> visit) {
      var collector = new EdgeCollector(this);
      visit(collector);
      if (collector.Dispatches) {
        if (reaching.Add(node)) {
          work.Push(node);
        }
      }
      edges[node] = collector.Callees;
    }
    foreach (var module in program.Modules()) {
      foreach (var decl in module.TopLevelDecls) {
        if (decl is IteratorDecl iter) {
          AddNode(iter, c => {
            c.Visit(iter.Requires);
            c.Visit(iter.Ensures);
            c.Visit(iter.YieldRequires);
            c.Visit(iter.YieldEnsures);
            c.Visit(iter.Decreases.Expressions);
            c.Visit(iter.Reads.Expressions);
            c.Visit(iter.Modifies.Expressions);
            c.Visit(iter.Ins.Where(f => f.DefaultValue != null).Select(f => f.DefaultValue));
            if (iter.Body != null) {
              c.Visit(iter.Body);
            }
          });
        }
        if (decl is not TopLevelDeclWithMembers cl) {
          continue;
        }
        foreach (var member in cl.Members) {
          switch (member) {
            case Function or MethodOrConstructor:
              AddNode(member, c => {
                c.Visit((ICallable)member);
                foreach (var clause in ModularTermination.GlobalClauses(((ICallable)member).Decreases.Attributes)) {
                  c.Visit(clause.Args);
                }
                // A default value is evaluated as part of the call that omits the argument.
                c.Visit(((ICallable)member).Ins.Where(f => f.DefaultValue != null).Select(f => f.DefaultValue));
              });
              break;
            case ConstantField { Rhs: { } rhs }:
              AddNode(member, c => c.Visit(rhs));
              break;
          }
        }
      }
    }
    // A call to a bodyless member of a trait that is not open is dispatched to one of its overrides.
    foreach (var module in program.Modules()) {
      foreach (var cl in module.TopLevelDecls.OfType<TopLevelDeclWithMembers>()) {
        foreach (var member in cl.Members) {
          if (member.OverriddenMember is { } parent && !IsOpen(parent)) {
            if (!edges.TryGetValue(parent, out var parentEdges)) {
              edges[parent] = parentEdges = [];
            }
            parentEdges.Add(member);
          }
        }
      }
    }
    foreach (var (node, callees) in edges) {
      foreach (var callee in callees) {
        if (!callers.TryGetValue(callee, out var list)) {
          callers[callee] = list = [];
        }
        list.Add(node);
      }
    }
    while (work.Count != 0) {
      var node = work.Pop();
      if (callers.TryGetValue(node, out var list)) {
        foreach (var caller in list) {
          if (reaching.Add(caller)) {
            work.Push(caller);
          }
        }
      }
    }
  }

  private bool IsOpen(MemberDecl member) {
    return ModularTermination.IsOpenDispatch(member, OpenTraits);
  }

  /// <summary>
  /// The node that a call of, or a reference to, "member" leads to: the iterator for its MoveNext, which runs
  /// the iterator's body, otherwise the member itself.
  /// </summary>
  private static Declaration NodeOf(MemberDecl member) {
    return member.EnclosingClass is IteratorDecl iter && member == iter.Member_MoveNext ? iter : member;
  }

  /// <summary>
  /// Whether a call of "callee" that is not itself an open dispatch may lead to one.
  /// </summary>
  public bool ReachesOpenDispatch(MemberDecl callee) {
    return reaching.Contains(NodeOf(callee));
  }

  private class EdgeCollector : BottomUpVisitor {
    private readonly ModularTerminationAnalysis analysis;
    public readonly HashSet<Declaration> Callees = [];
    public bool Dispatches;

    public EdgeCollector(ModularTerminationAnalysis analysis) {
      this.analysis = analysis;
    }

    private void Call(MemberDecl callee) {
      if (analysis.IsOpen(callee)) {
        Dispatches = true;
      } else {
        Callees.Add(NodeOf(callee));
      }
    }

    protected override void VisitOneExpr(Expression expr) {
      switch (expr) {
        case FunctionCallExpr call when call.Function != null:
          Call(call.Function);
          break;
        case MemberSelectExpr { Member: Function or MethodOrConstructor or ConstantField { Rhs: not null } } select:
          Call(select.Member);
          break;
      }
    }

    protected override void VisitOneStmt(Statement stmt) {
      if (stmt is CallStmt { Method: { } method }) {
        Call(method);
      }
    }
  }
}

/// <summary>
/// Under --modular-termination: checks the global decreases clauses, and reports, as hints, every
/// call that needs the smaller-bound rule.
/// </summary>
public class ModularTerminationRewriter : IRewriter {
  public ModularTerminationRewriter(ErrorReporter reporter) : base(reporter) { }

  internal override void PostResolve(Program program) {
    var analysis = new ModularTerminationAnalysis(program);
    foreach (var module in program.Modules()) {
      foreach (var decl in module.TopLevelDecls) {
        if (decl is not TopLevelDeclWithMembers cl) {
          continue;
        }
        foreach (var member in cl.Members) {
          if (member is not (MethodOrConstructor or Function) || !member.ShouldVerify(program.Compilation)) {
            continue;
          }
          var callable = (ICallable)member;
          CheckGlobalClauses(callable);
          var classifier = new CallClassifier(Reporter, callable, analysis);
          classifier.Visit(callable);
          classifier.Visit(callable.Ins.Where(f => f.DefaultValue != null).Select(f => f.DefaultValue));
        }
      }
    }
  }

  private void CheckGlobalClauses(ICallable callable) {
    var clauses = ModularTermination.GlobalClauses(callable.Decreases.Attributes).ToList();
    if (clauses.Count > 1) {
      Reporter.Error(MessageSource.Resolver, clauses[0].Origin,
        $"{callable.WhatKind} '{callable.NameRelativeToModule}' has more than one 'decreases {{:global}}' clause");
    }
  }

  private class CallClassifier : BottomUpVisitor {
    private readonly ErrorReporter reporter;
    private readonly ICallable caller;
    private readonly ModularTerminationAnalysis analysis;
    // A call can be reached more than once, for instance through a quantifier's triggers; it is
    // reported once, at its position.
    private readonly HashSet<(System.Uri, int, int)> reported = new();

    public CallClassifier(ErrorReporter reporter, ICallable caller, ModularTerminationAnalysis analysis) {
      this.reporter = reporter;
      this.caller = caller;
      this.analysis = analysis;
    }

    protected override void VisitOneExpr(Expression expr) {
      switch (expr) {
        case FunctionCallExpr call:
          ReportDispatch(call.Origin, call.Function);
          break;
        case ApplyExpr apply when !IsKnownCode(apply.Function):
          ReportCallback(apply.Origin, "a call through a function value");
          break;
        case SeqConstructionExpr seq when !IsKnownCode(seq.Initializer):
          ReportCallback(seq.Origin, "a sequence constructor's calls of its initializing function");
          break;
      }
    }

    protected override void VisitOneStmt(Statement stmt) {
      switch (stmt) {
        case CallStmt call:
          ReportDispatch(call.Origin, call.Method);
          break;
        case SingleAssignStmt { Rhs: AllocateArray { ElementInit: { } init } } assign when !IsKnownCode(init):
          ReportCallback(assign.Origin, "an array allocation's calls of its initializing function");
          break;
      }
    }

    private static bool IsKnownCode(Expression function) {
      return function.Resolved is LambdaExpr;
    }

    private bool FirstReport(IOrigin origin) {
      var center = origin.Center;
      return reported.Add((center.Uri, center.line, center.col));
    }

    private void ReportDispatch(IOrigin origin, MemberDecl callee) {
      var dispatched = ModularTermination.IsOpenDispatch(callee, analysis.OpenTraits);
      if (!dispatched && !analysis.ReachesOpenDispatch(callee)) {
        return;
      }
      if (!FirstReport(origin)) {
        return;
      }
      var calleeHasBound = ModularTermination.HasGlobalBound((ICallable)callee);
      var callerHasBound = ModularTermination.HasGlobalBound(caller);
      if (dispatched) {
        string status;
        if (!calleeHasBound) {
          status = "the callee has no 'decreases {:global}' bound, so the call is rejected";
        } else if (callerHasBound) {
          status = "checked: the callee's global bound must be below the caller's";
        } else {
          status = "holds: the caller has no 'decreases {:global}' bound, which is above every bound";
        }
        reporter.Info(MessageSource.Resolver, origin,
          $"modular termination: in {CallerName}, dispatch of '{callee.EnclosingClass.EnclosingModuleDefinition.Name}.{callee.EnclosingClass.Name}.{callee.Name}' to implementations " +
          $"that other modules may provide needs the smaller-bound rule; {status}");
        return;
      }
      string callerPart;
      if (callerHasBound) {
        callerPart = calleeHasBound ? "checked: it must not exceed the caller's" : "so the call is rejected";
      } else if (ModularTermination.ImplementsOpenDispatch((MemberDecl)caller, analysis.OpenTraits)) {
        callerPart = "and the caller has none until the trait member it implements gets one, as its dispatched calls require";
      } else {
        callerPart = "and the caller has none, which is above every bound";
      }
      reporter.Info(MessageSource.Resolver, origin,
        $"modular termination: in {CallerName}, a call of '{QualifiedName(callee)}', which can reach dispatch to implementations " +
        $"that other modules may provide, runs under the caller's bound (same-bound rule); " +
        $"the callee has {(calleeHasBound ? "a" : "no")} 'decreases {{:global}}' bound, {callerPart}");
    }

    private void ReportCallback(IOrigin origin, string what) {
      if (!FirstReport(origin)) {
        return;
      }
      reporter.Info(MessageSource.Resolver, origin,
        $"modular termination: in {CallerName}, {what} needs the smaller-bound rule; not checked (calls through function values are not yet supported)");
    }

    private string CallerName => $"'{QualifiedName((MemberDecl)caller)}'";

    private static string QualifiedName(MemberDecl member) {
      var cl = member.EnclosingClass;
      return $"{cl.EnclosingModuleDefinition.Name}.{(cl is DefaultClassDecl ? "" : cl.Name + ".")}{member.Name}";
    }
  }
}
