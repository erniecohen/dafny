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
/// A component "e by O" is allowed only in the decreases clause of a function, method or loop, and in the
/// "decreases {:global}" and "decreases {:callback f}" bounds of --modular-termination of a function or method,
/// and not with --legacy-decreases-order.
/// </summary>
public static class DecreasesOrders {
  public const string WfoModule = "Std.Termination.Orders";
  public const string WfoName = "Wfo";
  public const string GtName = "gt";
  public const string AhaName = "aha";
  public const string WfoOfName = "WfoOf";
  public const string OfName = "Of";
  public const string WellFoundedName = "wellFounded";
  public const string StopsName = "stops";

  /// <summary>
  /// Whether the options let a client hold values of the trait Wfo: a datatype value converts to a trait type
  /// only with the refreshed type system and general traits.  The orders library is loaded, and "e by O" is
  /// accepted, only then.
  /// </summary>
  public static bool TypeSystemSupportsOrders(DafnyOptions options) {
    return options.Get(CommonOptionBag.TypeSystemRefresh) &&
           options.Get(CommonOptionBag.GeneralTraits) != CommonOptionBag.GeneralTraitsOptions.Legacy;
  }

  /// <summary>
  /// Whether the orders library is loaded: with the standard library, or, under --modular-termination, which does
  /// not load the bundled standard library, on its own (it imports nothing).
  /// </summary>
  public static bool LoadsOrdersLibrary(DafnyOptions options) {
    return options.Get(CommonOptionBag.DecreasesOrders) && TypeSystemSupportsOrders(options) &&
           (options.Get(CommonOptionBag.UseStandardLibraries) || options.Get(CommonOptionBag.ModularTermination));
  }

  /// <summary>
  /// Whether "uri" is the orders library loaded under --modular-termination.  It has no modular termination record,
  /// so it is verified with the program that loads it, under that program's options, instead of being admitted.
  /// </summary>
  public static bool VerifiesOrdersLibrary(DafnyOptions options, System.Uri uri) {
    return options.Get(CommonOptionBag.ModularTermination) &&
           (uri == DafnyMain.StandardLibrariesOrdersDooUri || uri == DafnyMain.StandardLibrariesOrdersInternalUri);
  }

  public static bool IsStandardLibraryWfo(TopLevelDecl decl) {
    return decl is TraitDecl { Name: WfoName } && InOrdersLibrary(decl);
  }

  /// <summary>
  /// Whether "decl" is a declaration of the orders library's module.
  /// </summary>
  public static bool InOrdersLibrary(TopLevelDecl decl) {
    return decl.EnclosingModuleDefinition.FullDafnyName == WfoModule && decl.Origin.Uri == DafnyMain.StandardLibrariesOrdersDooUri;
  }

  /// <summary>
  /// Whether "decl" is the orders library's datatype WfoOf, the order whose gt is the relation it holds.
  /// </summary>
  public static bool IsLibraryWfoOf(TopLevelDecl decl) {
    return decl is DatatypeDecl { Name: WfoOfName } && InOrdersLibrary(decl);
  }

  /// <summary>
  /// Whether "function" is one of the orders library's module-level functions Of, wellFounded and stops, whose only
  /// code that is not known is the application of their arguments of arrow type.  Of makes a WfoOf from the relation
  /// it is given.
  /// </summary>
  public static bool AppliesOnlyItsArguments(Function function) {
    return function is { Name: OfName or WellFoundedName or StopsName, EnclosingClass: DefaultClassDecl cl } &&
           cl.EnclosingModuleDefinition.FullDafnyName == WfoModule && function.Origin.Uri == DafnyMain.StandardLibrariesOrdersDooUri;
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
    // The components of the ordinary clause, and (M4) those of the global and callback bounds of
    // --modular-termination, which the parser builds only under that option.
    var components = clause.Expressions.OfType<DecreasesByExpr>().ToList();
    for (var a = clause.Attributes; a != null; a = a.Prev) {
      if (ModularTermination.IsModularTerminationClause(a)) {
        components.AddRange(a.Args.OfType<DecreasesByExpr>());
      }
    }
    foreach (var e in components) {
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
