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
///
/// An order type (issue 211) is a subset type declared with an order, "type {:order O} L = x: T | C".  A
/// component of type L, and the bound of a bounded callable "bounded&lt;L, F&gt;", is compared as "e by O".  The type
/// names the order, so a bound stored in a value keeps it: O must meet R1 and R2 for T, and L has no type
/// parameters, since O is not instantiated with them.  R3 holds for every callable that uses an order type.
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
  public const string MultisetOfName = "MultisetOf";
  public const string MultisetRelName = "MultisetRel";
  public const string OrderAttributeName = "order";

  /// <summary>
  /// The order declared by "decl" with the attribute {:order O}, or null.
  /// </summary>
  public static Expression? DeclaredOrder(TopLevelDecl decl) {
    return decl is SubsetTypeDecl && Attributes.Find(decl.Attributes, OrderAttributeName) is { Args: [var order] } ? order : null;
  }

  /// <summary>
  /// If "type", keeping subset types, is an order type, its order; otherwise null.
  /// </summary>
  public static Expression? TypeOrder(Type? type) {
    return type?.NormalizeExpandKeepConstraints() is UserDefinedType { ResolvedClass: SubsetTypeDecl decl } ? DeclaredOrder(decl) : null;
  }

  /// <summary>
  /// The order by which the decreases component "e" is compared: that of "e by O", or else that of its order type;
  /// null if it has the built-in order of its type.
  /// </summary>
  public static Expression? ComponentOrder(Expression e) {
    return DecreasesByExpr.OrderOf(e) ?? TypeOrder(e.Type);
  }

  /// <summary>
  /// "e", compared by the order of the order type "declared" if "e" has no order of its own.  An argument
  /// substituted for a formal of an order type may have the base type instead (issue 211).
  /// </summary>
  public static Expression WithTypeOrder(Expression e, Type declared) {
    if (ComponentOrder(e) != null || TypeOrder(declared) is not { } order) {
      return e;
    }
    return new DecreasesByExpr(e.Origin, e, order) { ResolvedExpression = e, Type = e.Type };
  }

  /// <summary>
  /// The functions of the orders library's multiset order, other than MultisetOf and MultisetRel, that a client
  /// may call: the order itself and the base relations' properties its lemmas require.
  /// </summary>
  private static readonly HashSet<string> MultisetFunctionNames = new() {
    "MultisetGe", "MultisetGt", "Transitive"
  };

  /// <summary>
  /// The lemmas of the orders library's multiset order that a client calls to prove that a multiset goes down.
  /// </summary>
  private static readonly HashSet<string> MultisetLemmaNames = new() {
    "WellFoundedIrreflexive", "MultisetReplaceLower", "MultisetReplaceByLower", "MultisetReplaceOne",
    "MultisetAddOne", "MultisetSub", "MultisetGeAdd", "MultisetGeUnion", "MultisetGeCons", "MultisetDrop",
    "MultisetGeTransitive", "MultisetAntisymmetric", "MultisetGeGt", "MultisetGtGe", "MultisetTransitive"
  };

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
  /// Whether "function" is one of the orders library's module-level functions Of, wellFounded and stops, or of its
  /// multiset order (MultisetOf, MultisetRel and MultisetFunctionNames), whose only code that is not known is the
  /// application of their arguments of arrow type.  Of makes a WfoOf from the relation it is given, and MultisetOf
  /// makes one from the multiset order over the relation it is given.  The proofs that the multiset order is
  /// well-founded, which MultisetRel calls, are lemmas of the library that apply nothing but that relation and
  /// functions they build from it, and terminate by their own decreases clauses.
  /// </summary>
  public static bool AppliesOnlyItsArguments(Function function) {
    return (function.Name is OfName or WellFoundedName or StopsName or MultisetOfName or MultisetRelName ||
            MultisetFunctionNames.Contains(function.Name)) &&
           InOrdersLibraryModule(function);
  }

  /// <summary>
  /// Whether "lemma" is one of the orders library's lemmas of the multiset order (MultisetLemmaNames), which a client
  /// calls to prove that a multiset goes down.  Like the functions of AppliesOnlyItsArguments, they
  /// apply nothing but their argument of arrow type and functions they build from it.
  /// </summary>
  public static bool AppliesOnlyItsArguments(MethodOrConstructor lemma) {
    return lemma is Lemma && MultisetLemmaNames.Contains(lemma.Name) &&
           InOrdersLibraryModule(lemma);
  }

  /// <summary>
  /// Whether a call of "callee" runs no code but its arguments of arrow type: one of the functions or lemmas of
  /// AppliesOnlyItsArguments.  The caller's walk applies those arguments at the call site, so under
  /// --modular-termination the call itself needs no bound check.  This holds only for a call, which supplies the
  /// arguments; a function named as a value is classified by what it reaches.
  /// </summary>
  public static bool CallAppliesOnlyItsArguments(MemberDecl callee) {
    return callee switch {
      Function function => AppliesOnlyItsArguments(function),
      MethodOrConstructor lemma => AppliesOnlyItsArguments(lemma),
      _ => false
    };
  }

  /// <summary>
  /// Whether "member" is a module-level declaration of the orders library.
  /// </summary>
  static bool InOrdersLibraryModule(MemberDecl member) {
    return member.EnclosingClass is DefaultClassDecl cl && cl.EnclosingModuleDefinition.FullDafnyName == WfoModule &&
           member.Origin.Uri == DafnyMain.StandardLibrariesOrdersDooUri;
  }

  /// <summary>
  /// Whether "function" is the orders library's MultisetRel, whose result is a relation that applies only the
  /// argument of MultisetRel.  A call of MultisetRel is visited as an application of that argument, so applying its
  /// result runs no other code.
  /// </summary>
  public static bool IsLibraryMultisetRel(Function function) {
    return function.Name == MultisetRelName && AppliesOnlyItsArguments(function);
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
      if (decl is SubsetTypeDecl subsetType && Attributes.Find(decl.Attributes, OrderAttributeName) is { } orderAttribute) {
        CheckOrderType(options, reporter, subsetType, orderAttribute, legacy);
      }
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
    if (!legacy && TypeSystemSupportsOrders(options)) {
      CheckOrderTypeUses(reporter, declarations);
    }
  }

  /// <summary>
  /// The checks of an order type "type {:order O} L = x: T | C": one argument, R1 and R2 for T, no type parameters,
  /// and no {:axiom}, which would skip the check that O is well-formed.
  /// </summary>
  static void CheckOrderType(DafnyOptions options, ErrorReporter reporter, SubsetTypeDecl decl, Attributes attribute, bool legacy) {
    if (attribute.Args.Count != 1) {
      reporter.Error(MessageSource.Resolver, attribute.Origin, "the attribute {:order O} of an order type takes exactly one argument, the order");
      return;
    }
    var order = attribute.Args[0];
    if (legacy) {
      reporter.Error(MessageSource.Resolver, order.Origin, "an order type cannot be used with --legacy-decreases-order");
      return;
    }
    if (decl.TypeArgs.Count != 0) {
      reporter.Error(MessageSource.Resolver, decl.Origin, "an order type must not have type parameters");
    }
    if (Attributes.Contains(decl.Attributes, "axiom")) {
      reporter.Error(MessageSource.Resolver, decl.Origin, "an order type must not be declared {:axiom}");
    }
    // R1
    if (WfoTrait(order.Type) == null || WfoOf(order.Type) is not { } element) {
      reporter.Error(MessageSource.Resolver, order.Origin,
        $"the order O of an order type must have type {WfoModule}.{WfoName}<T> (got {order.Type})");
      return;
    }
    if (!decl.Var.Type.NormalizeExpandKeepConstraints().Equals(element.NormalizeExpandKeepConstraints(), true)) {
      reporter.Error(MessageSource.Resolver, order.Origin,
        $"the order O of an order type must order exactly its base type (got {WfoName}<{element}> for base type {decl.Var.Type})");
    }
    // R2
    var free = new HashSet<IVariable>();
    bool usesHeap = false, usesOldHeap = false;
    Type? usesThis = null;
    var heapAt = new HashSet<Label>();
    FreeVariablesUtil.ComputeFreeVariables(options, order, free, ref usesHeap, ref usesOldHeap, heapAt, ref usesThis, true);
    if (free.Count != 0 || usesHeap || usesOldHeap || heapAt.Count != 0 || usesThis != null) {
      var what = free.Count != 0 ? $"variable '{free.First().Name}'" : usesThis != null ? "'this'" : "the heap";
      reporter.Error(MessageSource.Resolver, order.Origin, $"the order O of an order type must be closed, but it mentions {what}");
    }
  }

  /// <summary>
  /// R3 for the callables of "declarations" that use an order type: a component of that type is compared by its
  /// order's gt, so the callable must not be mutually recursive with Wfo.gt, Wfo.aha or their overrides.
  /// </summary>
  static void CheckOrderTypeUses(ErrorReporter reporter, List<TopLevelDecl> declarations) {
    foreach (var callable in ModuleDefinition.AllCallables(declarations)) {
      if (callable is not Node node || FirstOrderTypeUse(node) is not { } use) {
        continue;
      }
      var wfo = WfoTrait(use.Order.Type)!;
      var graph = callable.EnclosingModule.CallGraph;
      if (graph.FindVertex(callable) == null) {
        continue;
      }
      var representative = graph.GetSCCRepresentative(callable);
      foreach (var callee in ImpliedCallees(wfo)) {
        if (graph.FindVertex(callee) != null && graph.GetSCCRepresentative(callee) == representative) {
          reporter.Error(MessageSource.Resolver, use.Origin,
            $"'{callable.NameRelativeToModule}' compares values of an order type, so it is treated as calling {WfoName}.{callee.Name}; " +
            $"it must not be mutually recursive with {WfoName}.{callee.Name} or its overrides");
          break;
        }
      }
    }
  }

  static (Expression Order, IOrigin Origin)? FirstOrderTypeUse(INode root) {
    var stack = new Stack<INode>();
    stack.Push(root);
    while (stack.Count != 0) {
      var node = stack.Pop();
      if (node is Expression e && TypeOrder(e.Type) is { } order && WfoTrait(order.Type) != null) {
        return (order, e.Origin);
      }
      foreach (var child in node.Children) {
        if (child != null) {
          stack.Push(child);
        }
      }
    }
    return null;
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
