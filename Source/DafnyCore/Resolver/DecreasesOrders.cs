#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// Resolver checks for decreases components "e by O" (--decreases-orders).  The verifier compares such a
/// component by O.gt(caller, callee), and trusts that O.gt is well-founded.  That trust rests on these restrictions:
///   R1. O has type Std.Termination.Orders.Wfo&lt;T&gt;, the trait of the orders library, whose aha member proves
///       gt well-founded, and e has exactly type T.
///   R2. O is closed: no local variables, parameters, "this" or heap.  So caller and callee see the same order.
///   R3. A callable with a component "e by O" is treated as calling Wfo.gt and Wfo.aha, as a requires clause is
///       treated as called by its function.  These implied calls have no arguments to decrease, so the callable
///       must not be mutually recursive with them, or with any override of them.
/// A component "e by O" is allowed only in the decreases clause of a function, method or loop, and not with
/// --legacy-decreases-order.
/// </summary>
public static class DecreasesOrders {
  public const string WfoModule = "Std.Termination.Orders";
  public const string WfoName = "Wfo";
  public const string GtName = "gt";
  public const string AhaName = "aha";

  /// <summary>
  /// Whether the options let a client hold values of the trait Wfo: a datatype value converts to a trait type
  /// only with the refreshed type system and general traits.  The orders library is loaded, and "e by O" is
  /// accepted, only then.
  /// </summary>
  public static bool TypeSystemSupportsOrders(DafnyOptions options) {
    return options.Get(CommonOptionBag.TypeSystemRefresh) &&
           options.Get(CommonOptionBag.GeneralTraits) != CommonOptionBag.GeneralTraitsOptions.Legacy;
  }

  public static bool IsStandardLibraryWfo(TopLevelDecl decl) {
    return decl is TraitDecl { Name: WfoName } wfo &&
           wfo.EnclosingModuleDefinition.FullDafnyName == WfoModule &&
           wfo.Origin.Uri == DafnyMain.StandardLibrariesOrdersDooUri;
  }

  /// <summary>
  /// If "type" is exactly the orders library's Wfo&lt;T&gt;, then that trait.
  /// </summary>
  public static TraitDecl? WfoTrait(Type? type) {
    return type?.NormalizeExpand() is UserDefinedType { ResolvedClass: TraitDecl trait, TypeArgs.Count: 1 } &&
           IsStandardLibraryWfo(trait) ? trait : null;
  }

  /// <summary>
  /// If "type" is exactly the orders library's Wfo&lt;T&gt;, then T.
  /// </summary>
  public static Type? WfoOf(Type? type) {
    return WfoTrait(type) != null ? ((UserDefinedType)type!.NormalizeExpand()).TypeArgs[0] : null;
  }

  public static Function Member(TraitDecl wfo, string name) {
    return wfo.Members.OfType<Function>().First(f => f.Name == name);
  }

  /// <summary>
  /// The members of Wfo that a callable with a component "e by O" implicitly calls (R3).
  /// </summary>
  public static IEnumerable<Function> ImpliedCallees(TraitDecl wfo) {
    yield return Member(wfo, GtName);
    yield return Member(wfo, AhaName);
  }

  /// <summary>
  /// Whether two orders denote the same value: the same resolved expression, of the same type.  Both are closed (R2).
  /// </summary>
  public static bool SameOrder(Expression o0, Expression o1) {
    return o0.Type.Equals(o1.Type, true) && Triggers.ExprExtensions.ExpressionEq(o0, o1);
  }

  public static void Check(DafnyOptions options, ErrorReporter reporter, List<TopLevelDecl> declarations) {
    var legacy = options.Get(CommonOptionBag.LegacyDecreasesOrder);
    foreach (var decl in declarations) {
      if (decl is IteratorDecl iter) {
        CheckClause(options, reporter, iter, iter.Decreases, false, legacy, "the decreases clause of an iterator");
        CheckBody(options, reporter, iter, iter.Body, legacy);
      }
      if (decl is TopLevelDeclWithMembers { Members: var members }) {
        foreach (var member in members) {
          switch (member) {
            case Function f:
              CheckClause(options, reporter, f, f.Decreases, true, legacy, null);
              CheckBody(options, reporter, f, f.ByMethodBody, legacy);
              break;
            case MethodOrConstructor m:
              CheckClause(options, reporter, m, m.Decreases, true, legacy, null);
              CheckBody(options, reporter, m, m.Body, legacy);
              break;
          }
        }
      }
    }
  }

  static void CheckBody(DafnyOptions options, ErrorReporter reporter, ICallable callable, Statement? body, bool legacy) {
    if (body == null) {
      return;
    }
    foreach (var stmt in body.DescendantsAndSelf) {
      if (stmt is LoopStmt loop) {
        CheckClause(options, reporter, callable, loop.Decreases, true, legacy, null);
      }
    }
  }

  static void CheckClause(DafnyOptions options, ErrorReporter reporter, ICallable callable, Specification<Expression> clause,
                          bool allowed, bool legacy, string? where) {
    for (var a = clause.Attributes; a != null; a = a.Prev) {
      if (ModularTermination.IsModularTerminationClause(a)) {
        foreach (var e in a.Args.OfType<DecreasesByExpr>()) {
          reporter.Error(MessageSource.Resolver, e.Origin,
            $"a decreases component 'e by O' is not allowed in a '{{:{a.Name}}}' clause");
        }
      }
    }
    foreach (var e in clause.Expressions.OfType<DecreasesByExpr>()) {
      if (!allowed) {
        reporter.Error(MessageSource.Resolver, e.Origin, $"a decreases component 'e by O' is not allowed in {where}");
      } else if (legacy) {
        reporter.Error(MessageSource.Resolver, e.Origin,
          "a decreases component 'e by O' cannot be used with --legacy-decreases-order");
      } else {
        CheckComponent(options, reporter, callable, e);
      }
    }
  }

  static void CheckComponent(DafnyOptions options, ErrorReporter reporter, ICallable callable, DecreasesByExpr e) {
    // R1
    if (WfoTrait(e.Order.Type) is not { } wfo || WfoOf(e.Order.Type) is not { } element) {
      reporter.Error(MessageSource.Resolver, e.Order.Origin,
        $"the order O of a decreases component 'e by O' must have type {WfoModule}.{WfoName}<T> (got {e.Order.Type})");
      return;
    }
    // e must have exactly the type T, constraints included: gt is well-founded only on T.
    if (!e.E.Type.NormalizeExpandKeepConstraints().Equals(element.NormalizeExpandKeepConstraints(), true)) {
      reporter.Error(MessageSource.Resolver, e.E.Origin,
        $"the expression of a decreases component 'e by O' must have exactly the element type of O (got {e.E.Type}, expected {element})");
    }
    // R2
    var free = new HashSet<IVariable>();
    bool usesHeap = false, usesOldHeap = false;
    Type? usesThis = null;
    var heapAt = new HashSet<Label>();
    FreeVariablesUtil.ComputeFreeVariables(options, e.Order, free, ref usesHeap, ref usesOldHeap, heapAt, ref usesThis, true);
    if (free.Count != 0 || usesHeap || usesOldHeap || heapAt.Count != 0 || usesThis != null) {
      var what = free.Count != 0 ? $"variable '{free.First().Name}'" : usesThis != null ? "'this'" : "the heap";
      reporter.Error(MessageSource.Resolver, e.Order.Origin,
        $"the order O of a decreases component 'e by O' must be closed, but it mentions {what}");
    }
      // R3
    var graph = callable.EnclosingModule.CallGraph;
    var representative = graph.GetSCCRepresentative(callable);
    foreach (var callee in ImpliedCallees(wfo)) {
      if (graph.FindVertex(callee) != null && graph.GetSCCRepresentative(callee) == representative) {
        reporter.Error(MessageSource.Resolver, e.Order.Origin,
          $"'{callable.NameRelativeToModule}' has a decreases component 'e by O', so it is treated as calling {WfoName}.{callee.Name}; " +
          $"it must not be mutually recursive with {WfoName}.{callee.Name} or its overrides");
        return;
      }
    }
  }
}
