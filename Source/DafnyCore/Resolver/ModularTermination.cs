#nullable enable
using System.Collections.Generic;
using System.Linq;

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
}

/// <summary>
/// Under --modular-termination: checks the global decreases clauses, and reports, as hints, every
/// call that needs the smaller-bound rule.
/// </summary>
public class ModularTerminationRewriter : IRewriter {
  public ModularTerminationRewriter(ErrorReporter reporter) : base(reporter) { }

  internal override void PostResolve(Program program) {
    var openTraits = ModularTermination.OpenTraits(program);
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
          new CallClassifier(Reporter, callable, openTraits).Visit(callable);
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
    private readonly HashSet<TraitDecl> openTraits;
    // A call can be reached more than once, for instance through a quantifier's triggers; it is
    // reported once, at its position.
    private readonly HashSet<(System.Uri, int, int)> reported = new();

    public CallClassifier(ErrorReporter reporter, ICallable caller, HashSet<TraitDecl> openTraits) {
      this.reporter = reporter;
      this.caller = caller;
      this.openTraits = openTraits;
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
      if (!ModularTermination.IsOpenDispatch(callee, openTraits) || !FirstReport(origin)) {
        return;
      }
      var calleeHasBound = ModularTermination.HasGlobalBound((ICallable)callee);
      var callerHasBound = ModularTermination.HasGlobalBound(caller);
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
    }

    private void ReportCallback(IOrigin origin, string what) {
      if (!FirstReport(origin)) {
        return;
      }
      reporter.Info(MessageSource.Resolver, origin,
        $"modular termination: in {CallerName}, {what} needs the smaller-bound rule; not checked (calls through function values are not yet supported)");
    }

    private string CallerName {
      get {
        var member = (MemberDecl)caller;
        var cl = member.EnclosingClass;
        return $"'{cl.EnclosingModuleDefinition.Name}.{(cl is DefaultClassDecl ? "" : cl.Name + ".")}{member.Name}'";
      }
    }
  }
}
