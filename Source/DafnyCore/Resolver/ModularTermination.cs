#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Microsoft.Dafny;

/// <summary>
/// Declaration-side facts retained by source views and refinement clones under --modular-termination.
/// Slot kind is fixed before export projection can erase a function body. HasEvaluation describes
/// structural evaluation hooks; an opaque type view conservatively hides those hooks.
/// </summary>
public sealed record TerminationSourceContract(string SlotId, bool IsDispatchSlot, bool HasEvaluation, bool IsExtensibleOwner);

/// <summary>Declared openness and public termination effects for modular source checking.</summary>
public static class ModularTermination {
  public const string ClosedAttributeName = "termination_closed";

  /// <summary>
  /// Whether the rules of termination across modules (https://github.com/erniecohen/dafny/issues/211) are in force:
  /// by default, unless the hidden --legacy-no-modular-termination restores the 4.11 rules, and always under
  /// --modular-termination.  When they are, every trait may be extended from any module, and the parser reads
  /// "decreases {:global} E" and "decreases {:callback f} E" as bounds.  The checks themselves run only in a program
  /// that uses the design (<see cref="Active"/>).
  /// </summary>
  public static bool RulesEnabled(DafnyOptions options) {
    return options.Get(CommonOptionBag.ModularTermination) || !options.Get(CommonOptionBag.LegacyNoModularTermination);
  }

  private static readonly ConditionalWeakTable<Program, StrongBox<bool>> UsesDesignCache = new();

  /// <summary>
  /// Whether the checks of modular termination run on "program": always under --modular-termination; otherwise, while
  /// the rules are enabled, when the program uses the design (<see cref="UsesDesign"/>).  A program that does not is
  /// one that 4.11 accepts or rejects as before, and is translated exactly as before.  Ask only once "program" is
  /// resolved.
  /// </summary>
  public static bool Active(DafnyOptions options, Program program) {
    if (options.Get(CommonOptionBag.ModularTermination)) {
      return true;
    }
    return RulesEnabled(options) && UsesDesignCache.GetValue(program, p => new StrongBox<bool>(UsesDesign(p))).Value;
  }

  /// <summary>
  /// Whether some declaration of the resolved "program", its libraries included, uses the design of issue 211: a global
  /// or callback bound, a {:termination_closed} promise, or a type that extends a trait of another module without
  /// either legacy form.  Without one of these, no caller has a bound, no code promises to be closed, and every
  /// implementation of a trait member lies in the trait's module or is covered by a legacy premise, so no check of
  /// the design can fail and none adds a proof obligation.
  /// </summary>
  public static bool UsesDesign(Program program) {
    return FirstDesignUse(program, null) != null;
  }

  /// <summary>
  /// The first declaration, among those whose file satisfies "inFile" (every file when it is null), that uses the
  /// design: a declared global or callback bound, a {:termination_closed} promise, or an unmarked extension of a trait
  /// of another module; with a description of the use.  Null when there is none.  Each member is located by its own
  /// file, since the default class of a module has no location of its own.
  /// </summary>
  public static (Declaration Declaration, string Use)? FirstDesignUse(Program program, Func<Uri, bool> inFile) {
    bool Located(Declaration declaration) {
      if (declaration.Origin?.Uri is not { } uri) {
        return inFile == null;
      }
      // The orders library promises {:termination_closed} for its relations; that a program loads it is no use of
      // the design.
      if (uri == DafnyMain.StandardLibrariesOrdersDooUri || uri == DafnyMain.StandardLibrariesOrdersInternalUri) {
        return false;
      }
      return inFile == null || inFile(uri);
    }
    foreach (var module in program.Modules()) {
      foreach (var decl in module.TopLevelDecls) {
        if (Located(decl)) {
          if (DesignUse(decl) is { } use) {
            return (decl, use);
          }
          if (decl is TopLevelDeclWithMembers extending && ExtendsForeignTraitUnmarked(extending)) {
            return (decl, "an extension of a trait of another module without a legacy form");
          }
        }
        if (decl is not TopLevelDeclWithMembers type) {
          continue;
        }
        foreach (var member in type.Members) {
          if (Located(member) && DesignUse(member) is { } memberUse) {
            return (member, memberUse);
          }
        }
      }
    }
    return null;
  }

  private static string DesignUse(Declaration declaration) {
    if (Attributes.Contains(declaration.Attributes, ClosedAttributeName)) {
      return "a {:" + ClosedAttributeName + "} promise";
    }
    if (DeclaresBound(declaration)) {
      return "a 'decreases {:global}' or 'decreases {:callback}' bound";
    }
    // Inspect resolved types as well as attributes: a bounded field, formal or expression is a
    // design use even when its owner declares no bound. Follow aliases and inferred expression types.
    var pending = new Stack<INode>([declaration]);
    var seen = new HashSet<INode>();
    while (pending.TryPop(out var node)) {
      if (!seen.Add(node)) {
        continue;
      }
      if (node is Type type && BoundedCallables.Payload(type) != null ||
          node is Expression expression && expression.Type != null && BoundedCallables.Payload(expression.Type) != null) {
        return "a bounded callable type";
      }
      foreach (var child in node.Children) {
        pending.Push(child);
      }
    }
    return null;
  }

  private static bool DeclaresBound(Declaration declaration) {
    // Only methods, functions and iterators have a decreases clause (ConstantField is an ICallable whose Decreases throws).
    var decreases = declaration switch {
      MethodOrFunction m => m.Decreases,
      IteratorDecl iter => iter.Decreases,
      _ => null
    };
    for (var a = decreases?.Attributes; a != null; a = a.Prev) {
      if (IsModularTerminationClause(a)) {
        return true;
      }
    }
    return false;
  }

  /// <summary>
  /// Whether "type" extends a trait of another module, other than object, with neither {:termination false} on the trait
  /// nor @AssumeCrossModuleTermination on "type": an extension that 4.11 rejects.
  /// </summary>
  public static bool ExtendsForeignTraitUnmarked(TopLevelDeclWithMembers type) {
    if (Attributes.Contains(type.Attributes, "AssumeCrossModuleTermination")) {
      return false;
    }
    return type.ParentTraitHeads.Any(trait => !trait.IsObjectTrait && trait.EnclosingModuleDefinition != type.EnclosingModuleDefinition &&
                                              !IsExtensible(trait));
  }

  /// <summary>
  /// Whether "trait" carries the legacy form {:termination false}.  Under --modular-termination every trait may be
  /// extended from any module; the legacy form only exempts the foreign implementations of its members that have no
  /// declared bound from the leaf rule, as an unchecked premise.
  /// </summary>
  public static bool IsExtensible(TraitDecl trait) {
    bool termination = true;
    return !trait.IsObjectTrait && Attributes.ContainsBool(trait.Attributes, "termination", ref termination) && !termination;
  }

  /// <summary>
  /// Whether "member" is an overridable instance slot of a trait: a call of it is dispatched.
  /// </summary>
  public static bool IsDispatchSlot(MemberDecl member) {
    if (member.IsStatic) {
      return false;
    }
    if (member.TerminationContract is { } contract) {
      return contract.IsDispatchSlot && contract.IsExtensibleOwner;
    }
    return member.EnclosingClass is TraitDecl { IsObjectTrait: false } && member switch {
      Function f => f.Body == null,
      Method => true,
      _ => false
    };
  }

  public static bool HasClosedContract(Declaration declaration) {
    if (OpaqueMemberRewriter.IsGeneratedFunctionRevealLemma(declaration)) {
      return true;
    }
    if (Attributes.Contains(declaration.Attributes, ClosedAttributeName)) {
      return true;
    }
    return declaration switch {
      MemberDecl member => (member.OverriddenMember != null && HasClosedContract(member.OverriddenMember)) ||
                           (member.RefinementBase != null && HasClosedContract(member.RefinementBase)),
      TopLevelDecl { CardinalityRefinementBase: { } parent } => HasClosedContract(parent),
      _ => false
    };
  }

  public static ModuleDefinition ModuleOf(Declaration declaration) {
    return declaration is MemberDecl member ? member.EnclosingClass.EnclosingModuleDefinition :
      ((TopLevelDecl)declaration).EnclosingModuleDefinition;
  }

  /// <summary>
  /// Whether code of "module" sees "declaration" from outside: by its contract, not by its body.  The members of
  /// a recursive module group (--recursive-modules) are verified as one module would be, and a call from one
  /// member to another is checked against the "decreases" clauses, so a declaration of another member of the
  /// group of "module" is not seen from outside.
  /// </summary>
  public static bool IsOutside(Declaration declaration, ModuleDefinition module) {
    return !ModuleDefinition.InSameRecursiveGroup(ModuleOf(declaration), module);
  }

  public const string GlobalAttributeName = "global";

  public static bool IsGlobalClause(Attributes a) {
    return a.Name == GlobalAttributeName && a.Args is { Count: > 0 } && a is not UserSuppliedAtAttribute;
  }

  /// <summary>
  /// "decreases {:callback f} E" (milestone M3b): the function value that the in-parameter f holds runs under the
  /// global bound E, a list of expressions in the callable's in-parameters, evaluated on entry.  The parser keeps it
  /// as the attribute {:callback f, E}, as it keeps "decreases {:global} E" as {:global E}.
  /// </summary>
  public const string CallbackAttributeName = "callback";

  public static bool IsCallbackClause(Attributes a) {
    return a.Name == CallbackAttributeName && a.Args is { Count: > 1 } && a is not UserSuppliedAtAttribute;
  }

  /// <summary>
  /// Whether "a" is a global clause or a callback clause: a clause of --modular-termination, kept apart from the
  /// ordinary decreases clause.
  /// </summary>
  public static bool IsModularTerminationClause(Attributes a) {
    return IsGlobalClause(a) || IsCallbackClause(a);
  }

  public static IEnumerable<Attributes> CallbackClauses(Attributes? attrs) {
    for (var a = attrs; a != null; a = a.Prev) {
      if (IsCallbackClause(a)) {
        yield return a;
      }
    }
  }

  /// <summary>
  /// The in-parameter that a callback clause names, or null if its first argument names none of "callable"'s.
  /// </summary>
  public static Formal? CallbackParameter(ICallable callable, Attributes clause) {
    return clause.Args[0].Resolved is IdentifierExpr { Var: Formal f } && callable.Ins.Contains(f) ? f : null;
  }

  /// <summary>
  /// The callback bound that "callable" declares itself for its in-parameter "formal", or null.
  /// </summary>
  public static List<Expression>? DeclaredCallbackBound(ICallable callable, Formal formal) {
    if (callable is not (MethodOrConstructor or Function)) {
      return null;
    }
    return CallbackClauses(callable.Decreases.Attributes).LastOrDefault(a => CallbackParameter(callable, a) == formal)?.Args.Skip(1).ToList();
  }

  /// <summary>
  /// Whether the in-parameter at "index" of "callable" has a callback bound, declared or inherited.  The method of a
  /// function's "by method" body has the function's callback bounds.
  /// </summary>
  public static bool HasCallbackBound(ICallable callable, int index) {
    if (callable is Method { FunctionFromWhichThisIsByMethodDecl: { } function }) {
      return HasCallbackBound(function, index);
    }
    for (ICallable? c = callable; c != null; c = (c as MemberDecl)?.OverriddenMember as ICallable) {
      if (index < c.Ins.Count && DeclaredCallbackBound(c, c.Ins[index]) != null) {
        return true;
      }
    }
    return false;
  }

  public static bool HasCallbackBounds(ICallable callable) {
    return Enumerable.Range(0, callable.Ins.Count).Any(i => HasCallbackBound(callable, i));
  }

  /// <summary>
  /// Whether "function" is the "requires" or "reads" member of an arrow type, which a call applies to the receiver.
  /// </summary>
  public static string QualifiedName(MemberDecl member) {
    var cl = member.EnclosingClass;
    return $"{cl.EnclosingModuleDefinition.Name}.{(cl is DefaultClassDecl ? "" : cl.Name + ".")}{member.Name}";
  }

  public static bool IsArrowMember(MemberDecl function) {
    return function is Function && function.EnclosingClass is ArrowTypeDecl;
  }

  /// <summary>
  /// Whether "type" is a total arrow type "A -> B", rather than a partial "A --> B" or general "A ~> B" one.
  /// </summary>
  public static bool IsTotalArrow(Type type) {
    return type.NormalizeExpandKeepConstraints() is UserDefinedType { ResolvedClass: SubsetTypeDecl { Name: var name } } &&
           ArrowType.IsTotalArrowTypeName(name);
  }

  /// <summary>
  /// Issue 266: the function that "value" names, if it is one that "bounded.certify" may take as an exact
  /// implementation (design section 3.2).  Its own code runs, so it must not be an abstract trait member, whose
  /// implementation dispatch chooses; it must not have parameters with callback bounds, whose first-class protocol
  /// is not represented; and its global bound, declared or inherited, must depend on neither its parameters nor the
  /// heap, so that the bound of every later application is the one that the verifier checks, at "bounded.certify",
  /// to be at most the allowance.  If "value" names a function that cannot be admitted, "problem" says why.
  /// </summary>
  public static Function? AdmittedMember(DafnyOptions options, Expression value, out string? problem) {
    problem = null;
    if (value.Resolved is not MemberSelectExpr { Member: Function g } || IsArrowMember(g)) {
      return null;
    }
    if (g.EnclosingClass is TraitDecl && g.Body == null && !g.IsStatic) {
      problem = "it is a trait member, whose implementation dispatch chooses (certify a lambda that calls it)";
      return null;
    }
    if (HasCallbackBounds(g)) {
      problem = "it has a parameter with a 'decreases {:callback}' bound";
      return null;
    }
    List<Expression>? bound = null;
    for (ICallable? c = g; c != null && bound == null; c = (c as MemberDecl)?.OverriddenMember as ICallable) {
      bound = DeclaredGlobalBound(c);
    }
    if (bound == null) {
      problem = "it has no 'decreases {:global}' bound";
      return null;
    }
    foreach (var e in bound) {
      var usesHeap = false;
      var usesOldHeap = false;
      Type? usesThis = null;
      var fvs = new HashSet<IVariable>();
      FreeVariablesUtil.ComputeFreeVariables(options, e, fvs, ref usesHeap, ref usesOldHeap, new HashSet<Label>(), ref usesThis, false);
      if (fvs.Count != 0 || usesHeap || usesOldHeap) {
        problem = "its 'decreases {:global}' bound depends on its parameters or on the heap (certify a lambda with a narrower precondition)";
        return null;
      }
    }
    return g;
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
  /// The traits declared extensible by their defining module, independently of clients or descendants.
  /// </summary>
  public static HashSet<TraitDecl> OpenTraits(Program program) {
    return program.Modules().SelectMany(m => m.TopLevelDecls).OfType<TraitDecl>()
      .Where(IsExtensible).ToHashSet();
  }

  /// <summary>
  /// Whether a call to "callee" is dispatched to an implementation that may lie in a module that no
  /// call graph sees: "callee" is an overridable instance slot of a trait (every trait may be extended from any
  /// module, rule 1 of revision 3).  Such a call uses the callee's global bound, or TOP if it has none (rule 4).
  /// </summary>
  public static bool IsOpenDispatch(MemberDecl callee, HashSet<TraitDecl> openTraits) {
    return IsDispatchSlot(callee);
  }

  /// <summary>
  /// The member, among "member"'s ancestors, whose foreign implementations must be leaves (rule 3), if "member" is
  /// such a foreign implementation: an ancestor with no global bound, in a trait without the
  /// legacy form {:termination false}, declared in another module than "member".  A type with the legacy form
  /// @AssumeCrossModuleTermination is exempt, as an unchecked premise (section 2.4 of the plan).
  /// Another member of the recursive module group of "member" counts as the same module: the calls dispatched
  /// to an implementation in the group are checked against the "decreases" clauses, as they are inside one module.
  /// </summary>
  public static MemberDecl? UndeclaredForeignAncestor(MemberDecl member) {
    if (Attributes.Contains(member.EnclosingClass.Attributes, "AssumeCrossModuleTermination")) {
      return null;
    }
    for (var a = member.OverriddenMember; a != null; a = a.OverriddenMember) {
      if (a.EnclosingClass is TraitDecl trait && !IsExtensible(trait) && a is ICallable callable && !HasGlobalBound(callable) &&
          IsOutside(a, ModuleOf(member)) && !CoveredByLegacyPremise(member.EnclosingClass, trait)) {
        return a;
      }
    }
    return null;
  }

  /// <summary>
  /// Whether some path of extension from "type" to its ancestor "target" crosses modules at an edge that carries a
  /// legacy form: @AssumeCrossModuleTermination on the child, or {:termination false} on the parent.  Dafny 4.11
  /// checks only these crossing edges, so a type that extends, within its own module, a trait that extends a trait
  /// of another module under a legacy form (as Std.Actions.FunctionAction extends Action, which extends
  /// Frames.Validatable with @AssumeCrossModuleTermination) implements the foreign members under that premise.
  /// </summary>
  public static bool CoveredByLegacyPremise(TopLevelDecl type, TraitDecl target) {
    if (type is not TopLevelDeclWithMembers child) {
      return false;
    }
    foreach (var parent in child.ParentTraitHeads) {
      if (parent != target && !ExtendsTransitively(parent, target, new HashSet<TraitDecl>())) {
        continue;
      }
      if (parent.EnclosingModuleDefinition != child.EnclosingModuleDefinition &&
          (Attributes.Contains(child.Attributes, "AssumeCrossModuleTermination") || IsExtensible(parent))) {
        return true;
      }
      if (parent != target && CoveredByLegacyPremise(parent, target)) {
        return true;
      }
    }
    return false;
  }

  private static bool ExtendsTransitively(TraitDecl trait, TraitDecl target, HashSet<TraitDecl> seen) {
    if (!seen.Add(trait)) {
      return false;
    }
    return trait.ParentTraitHeads.Any(parent => parent == target || ExtendsTransitively(parent, target, seen));
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
/// At an import boundary only the public Closed/General contract is consulted; hidden body
/// reachability never supplies a stronger contract. Within a module the graph infers closure.
///
/// The nodes are the methods, functions and constants with initializers of every class, trait and datatype, and
/// the iterators.  The edges are the calls in their bodies, specifications, global bounds and the default values
/// of their parameters (evaluated as part of a call that omits the argument); a reference to a
/// function or method as a value; a reference to a constant; from a call of an iterator's MoveNext, to the iterator;
/// and from a bodyless member of a trait that is not open to each member that overrides it.  A call through a
/// function value with unknown code seeds the General effect directly.
/// </summary>
public class ModularTerminationAnalysis {
  private static readonly ConditionalWeakTable<Program, ModularTerminationAnalysis> Cache = new();

  public readonly HashSet<TraitDecl> OpenTraits;
  private readonly HashSet<Declaration> reaching = [];
  // The nodes that are not leaves (rule 3 of revision 3): those that reach open dispatch, an unknown application or
  // an imported General contract, themselves or through the code they call.
  private readonly HashSet<Declaration> nonLeaf = [];
  private readonly Dictionary<ModuleDefinition, VisibilityScope> interfaceScopes = new();
  private readonly ModuleDefinition systemModule;
  // The callees of each node, and, for a node that makes an open dispatch itself, a description of the first one.
  private readonly Dictionary<Declaration, HashSet<Declaration>> edges = new();
  private readonly Dictionary<Declaration, string> directDispatch = new();
  // Issue 211: the nodes that apply the argument-dependent bound of a bounded value, or call code that does, in any
  // module.  The argument-dependent bound given to "bounded.certify" may not be one of them (see AppliesArgumentBound).
  private readonly HashSet<Declaration> appliesArgumentBound = [];
  private readonly Dictionary<Declaration, HashSet<Declaration>> allEdges = new();
  private readonly List<MemberDecl> orderImplementations = [];
  private readonly HashSet<IVariable> logicalBinders = [];
  private readonly HashSet<MemberDecl> argumentSummaries;

  public bool AppliesOnlyItsArguments(MemberDecl member) =>
    DecreasesOrders.IsIntrinsicArgumentApplication(member) || argumentSummaries.Contains(member);

  /// <summary>
  /// The analysis of the program, computed once, the first time it is asked for.  Use it only once the
  /// program is fully resolved.
  /// </summary>
  public static ModularTerminationAnalysis Of(Program program) {
    return Cache.GetValue(program, p => {
      // The analysis is computed the first time some module's translation asks for it, while that module's
      // visibility scope is current, but it visits every module of the program.  Normalizing a type from a module
      // that scope cannot see (for instance one an export set only provides) fails a visibility assertion, so the
      // analysis runs with no scope, which sees every declaration as it is.
      var saved = Type.SaveScopes();
      Type.ResetScopes();
      try {
        return new ModularTerminationAnalysis(p);
      } finally {
        Type.RestoreScopes(saved);
      }
    });
  }

  public ModularTerminationAnalysis(Program program) {
    // RulesEnabled includes the default modular rules; the legacy opt-out disables this extension.
    argumentSummaries = program.Options.Get(CommonOptionBag.DecreasesOrders) &&
                        ModularTermination.RulesEnabled(program.Options)
      ? ArgumentApplicationSummary.Compute(program) : [];
    OpenTraits = ModularTermination.OpenTraits(program);
    systemModule = program.SystemModuleManager.SystemModule;
    foreach (var module in program.Modules()) {
      var scope = new VisibilityScope();
      scope.Augment(module.VisibilityScope);
      scope.Augment(program.SystemModuleManager.SystemModule.VisibilityScope);
      foreach (var import in module.TopLevelDecls.OfType<ModuleDecl>().Where(d => d is not ModuleExportDecl)) {
        scope.Augment(import.AccessibleSignature().VisibilityScope);
      }
      interfaceScopes[module] = scope;
    }
    // The implementations of Wfo.gt and Wfo.aha that a component "e by O" may run, other than those of the orders
    // library's WfoOf: the only code those run is the relation that WfoOf holds, which was applied where the WfoOf
    // was made (see EdgeCollector), in code that O, a closed term, reaches.
    foreach (var cl in program.Modules().SelectMany(m => m.TopLevelDecls).OfType<TopLevelDeclWithMembers>()) {
      if (DecreasesOrders.IsLibraryWfoOf(cl)) {
        continue;
      }
      foreach (var member in cl.Members) {
        for (var parent = member.OverriddenMember; parent != null; parent = parent.OverriddenMember) {
          if (parent.EnclosingClass is TraitDecl trait && DecreasesOrders.IsStandardLibraryWfo(trait) &&
              parent.Name is DecreasesOrders.GtName or DecreasesOrders.AhaName) {
            orderImplementations.Add(member);
            break;
          }
        }
      }
    }
    // The binder rule (https://github.com/erniecohen/dafny/issues/245, M4): a variable of a total arrow type bound
    // by a forall or exists expression, or by a ghost forall statement, ranges over every function of its type, all of
    // which exist before the code being analyzed.  Applying it is a formula over those functions, not a call, so it
    // is not a function value whose code is not known.  A quantifier over an arrow type has no bounds, so the resolver
    // admits such an expression only in a ghost context.
    program.Visit((INode node) => {
      var binders = node switch {
        QuantifierExpr quantifier => quantifier.BoundVars,
        ForallStmt { IsGhost: true } forall => forall.BoundVars,
        _ => null
      };
      foreach (var binder in binders ?? []) {
        if (ModularTermination.IsTotalArrow(binder.Type)) {
          logicalBinders.Add(binder);
        }
      }
      return true;
    });
    var callers = new Dictionary<Declaration, List<Declaration>>();
    var work = new Stack<Declaration>();
    void AddNode(Declaration node, System.Action<EdgeCollector> visit) {
      var collector = new EdgeCollector(this, ModularTermination.ModuleOf(node), node as ICallable);
      if (node is TopLevelDeclWithMembers type) {
        foreach (var parent in type.Traits) {
          collector.Mention(parent);
        }
      }
      visit(collector);
      if (collector.Dispatches) {
        directDispatch[node] = collector.DispatchDescription!;
        if (reaching.Add(node)) {
          work.Push(node);
        }
      }
      edges[node] = collector.Callees;
      allEdges[node] = collector.AllCallees;
      if (collector.AppliesArgumentBound) {
        appliesArgumentBound.Add(node);
      }
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
        // A type is a node too: its constraint and witness, and the default values of its components, are
        // evaluated by code that mentions it (see TypeMentions).
        switch (decl) {
          case NonNullTypeDecl:
            break;
          case SubsetTypeDecl subset:
            AddNode(subset, c => {
              c.Mention(subset.Var?.Type);
              if (subset.Constraint != null) {
                c.Visit(subset.Constraint);
              }
              if (subset.Witness != null) {
                c.Visit(subset.Witness);
              }
            });
            break;
          case TypeSynonymDecl synonym:
            AddNode(synonym, c => c.Mention(synonym.Rhs));
            break;
          case NewtypeDecl newtype:
            AddNode(newtype, c => {
              c.Mention(newtype.BaseType);
              if (newtype.Constraint != null) {
                c.Visit(newtype.Constraint);
              }
              if (newtype.Witness != null) {
                c.Visit(newtype.Witness);
              }
            });
            break;
          case DatatypeDecl datatype:
            AddNode(datatype, c => {
              foreach (var formal in datatype.Ctors.SelectMany(ctor => ctor.Formals)) {
                c.Mention(formal.Type);
                if (formal.DefaultValue != null) {
                  c.Visit(formal.DefaultValue);
                }
              }
            });
            break;
          case ClassLikeDecl classLike when classLike is not IteratorDecl:
            AddNode(classLike, c => {
              foreach (var field in classLike.Members.OfType<Field>()) {
                c.Mention(field.Type);
              }
            });
            break;
        }
        if (decl is not TopLevelDeclWithMembers cl) {
          continue;
        }
        foreach (var member in cl.Members) {
          switch (member) {
            case Function or MethodOrConstructor:
              AddNode(member, c => {
                c.MentionSignature((ICallable)member);
                c.Visit((ICallable)member);
                foreach (var clause in ModularTermination.GlobalClauses(((ICallable)member).Decreases.Attributes)) {
                  c.Visit(clause.Args);
                }
                // A default value is evaluated as part of the call that omits the argument.
                c.Visit(((ICallable)member).Ins.Where(f => f.DefaultValue != null).Select(f => f.DefaultValue));
                // A member without a body may be given one by a refining module, and code inherited from this module,
                // whose calls were checked here, is not checked again there.  So, unless it promises
                // {:termination_closed}, which a refinement must keep, it is taken to reach a dispatch.
                if (member is Function { Body: null } or MethodOrConstructor { Body: null } &&
                    !ModularTermination.IsDispatchSlot(member) && !ModularTermination.HasClosedContract(member)) {
                  c.BodyToCome();
                }
              });
              break;
            case ConstantField { Rhs: { } rhs } constant:
              AddNode(member, c => {
                c.Mention(constant.Type);
                c.Visit(rhs);
              });
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
          if (member.OverriddenMember is { } overridden) {
            if (!allEdges.TryGetValue(overridden, out var overriddenEdges)) {
              allEdges[overridden] = overriddenEdges = [];
            }
            overriddenEdges.Add(member);
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
    var allCallers = new Dictionary<Declaration, List<Declaration>>();
    foreach (var (node, callees) in allEdges) {
      foreach (var callee in callees) {
        if (!allCallers.TryGetValue(callee, out var list)) {
          allCallers[callee] = list = [];
        }
        list.Add(node);
      }
    }
    foreach (var node in appliesArgumentBound) {
      work.Push(node);
    }
    while (work.Count != 0) {
      var node = work.Pop();
      foreach (var caller in allCallers.GetValueOrDefault(node, [])) {
        if (appliesArgumentBound.Add(caller)) {
          work.Push(caller);
        }
      }
    }
    // A node that reaches open dispatch is not a leaf; nor is one that calls a node that is not a leaf.
    foreach (var node in reaching) {
      nonLeaf.Add(node);
    }
    foreach (var node in nonLeaf) {
      work.Push(node);
    }
    while (work.Count != 0) {
      var node = work.Pop();
      if (callers.TryGetValue(node, out var list)) {
        foreach (var caller in list) {
          if (nonLeaf.Add(caller)) {
            work.Push(caller);
          }
        }
      }
    }
  }

  /// <summary>
  /// Whether "declaration" is not a leaf (rule 3 of revision 3): it, or the code it calls, makes a dispatched call
  /// through a trait, applies a function value whose code is not known, or depends on code of another module that
  /// does not promise to be a leaf.
  /// </summary>
  public bool IsNonLeaf(Declaration declaration) => nonLeaf.Contains(NodeOf(declaration));

  /// <summary>
  /// If "declaration" is not a leaf, the shortest chain of code by which it is not, for instance
  /// "'Y.Y.A' -> 'Tr.G' -> dispatch of 'Tr.T.A'"; else null.
  /// </summary>
  public string? NonLeafChain(Declaration declaration) {
    var start = NodeOf(declaration);
    if (!nonLeaf.Contains(start)) {
      return null;
    }
    var parent = new Dictionary<Declaration, Declaration?> { [start] = null };
    var queue = new Queue<Declaration>();
    queue.Enqueue(start);
    while (queue.Count != 0) {
      var node = queue.Dequeue();
      var direct = directDispatch.GetValueOrDefault(node);
      if (direct != null) {
        var chain = new List<string> { direct };
        for (Declaration? n = node; n != null; n = parent[n]) {
          chain.Add(NodeName(n));
        }
        chain.Reverse();
        return string.Join(" -> ", chain);
      }
      foreach (var next in edges.GetValueOrDefault(node, []).Where(nonLeaf.Contains).OrderBy(NodeName, StringComparer.Ordinal)) {
        if (parent.TryAdd(next, node)) {
          queue.Enqueue(next);
        }
      }
    }
    return NodeName(start);
  }

  private bool IsOpen(MemberDecl member) {
    return ModularTermination.IsOpenDispatch(member, OpenTraits);
  }

  /// <summary>
  /// The source owner of an evaluation: the iterator for its constructor/Valid/MoveNext protocol, the extreme
  /// predicate or lemma for its prefix predicate or lemma, otherwise the member itself.
  /// </summary>
  private static Declaration NodeOf(Declaration declaration) {
    return declaration is MemberDecl member ? NodeOf(member) : declaration;
  }

  private static Declaration NodeOf(MemberDecl member) {
    return member switch {
      // A prefix predicate or lemma is its extreme member's body, run to a depth _k.
      PrefixPredicate prefix => prefix.ExtremePred,
      PrefixLemma prefix => prefix.ExtremeLemma,
      _ => member.EnclosingClass is IteratorDecl iter &&
           (member == iter.Member_Init || member == iter.Member_Valid || member == iter.Member_MoveNext) ? iter : member
    };
  }

  /// <summary>
  /// Whether a call of "callee" that is not itself an open dispatch may lead to one.
  /// </summary>
  public bool ReachesOpenDispatch(MemberDecl callee, ModuleDefinition? callerModule = null) {
    var node = NodeOf(callee);
    return callerModule != null && ModularTermination.IsOutside(node, callerModule)
      ? HasGeneralContract(node, callerModule) : reaching.Contains(node);
  }

  private bool HasGeneralContract(Declaration node, ModuleDefinition callerModule) {
    return !ModularTermination.HasClosedContract(node) && HasEvaluationHooks(node, callerModule);
  }

  /// <summary>
  /// Whether depending on "node", from "callerModule", may run code of "node"'s module: then, across modules, only a
  /// public promise says what that code may reach.
  /// </summary>
  private bool HasEvaluationHooks(Declaration node, ModuleDefinition callerModule) {
    // Built-in types and operations have no provider-defined evaluation hooks. Arrow
    // requires/reads are checked as applications of their receiver, not imported helpers.
    if (ModularTermination.ModuleOf(node) == systemModule) {
      return false;
    }
    var hiddenType = node is TopLevelDecl && interfaceScopes.TryGetValue(callerModule, out var scope) &&
                     !node.IsRevealedInScope(scope);
    var structuralHook = node is TopLevelDeclWithMembers type &&
                         (type.Members.OfType<Field>().Any() || type.Traits.Count != 0);
    return node is MemberDecl || node is AbstractTypeDecl || hiddenType || structuralHook ||
           node.TerminationContract is { HasEvaluation: true };
  }

  private bool NeedsGeneral(Declaration node, ModuleDefinition? callerModule) {
    return callerModule != null && ModularTermination.IsOutside(node, callerModule)
      ? HasGeneralContract(node, callerModule) : reaching.Contains(node);
  }

  public bool ReachingDeclaration(Declaration declaration) => reaching.Contains(declaration);

  /// <summary>
  /// Whether evaluating "exprs" may apply the argument-dependent bound of a bounded value (issue 211).  Such a bound
  /// is a function that a later bound applies, so if it could apply one in turn, a bound read from an object's
  /// constant could apply itself, as in "o =&gt; {1} + o.f.bound(o)", and its definition would be a contradiction.  So
  /// the bound given to "bounded.certify" computes from data alone, as VeriFast's levels do.
  /// </summary>
  public bool AppliesArgumentBound(IEnumerable<Expression> exprs) {
    var collector = new EdgeCollector(this, null);
    collector.Visit(exprs);
    return collector.AppliesArgumentBound || collector.AllCallees.Any(appliesArgumentBound.Contains);
  }

  public enum FunctionValueKind {
    /// <summary>A lambda expression: its body is part of the code that creates it, and is checked there.</summary>
    Lambda,
    /// <summary>A function or method named as a value that reaches no open dispatch.</summary>
    KnownCode,
    /// <summary>An in-parameter of the enclosing callable that has a callback bound (M3b).</summary>
    CallbackParameter,
    /// <summary>
    /// A variable of a total arrow type bound by a quantifier or a ghost forall statement (the binder rule of issue 245
    /// M4): applying it calls nothing.  It is not a value that may be passed for a parameter with a callback bound.
    /// </summary>
    Binder,
    /// <summary>
    /// The function value "X.erase" of a value "X" of a bounded type (issue 266): it runs under "X.bound", which the
    /// verifier checks.
    /// </summary>
    Bounded,
    /// <summary>Any other value: its code is not known, so it has no bound (TOP).</summary>
    Unknown
  }

  /// <summary>
  /// What runs when the function value "value" is applied in "enclosing" (M3b).
  /// </summary>
  public FunctionValueKind Classify(Expression value, ICallable? enclosing) {
    switch (value.Resolved) {
      case LambdaExpr:
        return FunctionValueKind.Lambda;
      case var erased when BoundedCallables.ErasedCallable(erased) != null:
        return FunctionValueKind.Bounded;
      case var bound when BoundedCallables.IsArgumentBound(bound):
        return FunctionValueKind.KnownCode;
      case IdentifierExpr { Var: { } binder } when logicalBinders.Contains(binder):
        return FunctionValueKind.Binder;
      case IdentifierExpr { Var: Formal f } when enclosing != null && enclosing.Ins.IndexOf(f) is var i and >= 0 &&
                                                  ModularTermination.HasCallbackBound(enclosing, i):
        return FunctionValueKind.CallbackParameter;
      case MemberSelectExpr { Member: Function g } when !ModularTermination.IsArrowMember(g) && !IsOpen(g) &&
                                                         !ReachesOpenDispatch(g, enclosing == null ? null : enclosing.EnclosingModule) && !ModularTermination.HasCallbackBounds(g):
        return FunctionValueKind.KnownCode;
      default:
        return FunctionValueKind.Unknown;
    }
  }

  /// <summary>
  /// The first type declaration among "types" whose constraint, witness or components' default values may lead to
  /// open dispatch, or null.
  /// </summary>
  public TopLevelDecl? ReachingType(IEnumerable<Type> types, ModuleDefinition? callerModule = null) {
    return types.SelectMany(TypeMentions.Declarations).FirstOrDefault(d => NeedsGeneral(d, callerModule));
  }

  /// <summary>
  /// M4a: if the expressions "exprs" (a global or callback bound) can reach open dispatch, the shortest chain of
  /// code by which they do, ending in the dispatch itself, for instance "'Z.R' -> dispatch of 'Tr.T.F'"; else null.
  /// </summary>
  public string? ReachingChain(IEnumerable<Expression> exprs, ModuleDefinition? callerModule = null) {
    var collector = new EdgeCollector(this, callerModule);
    collector.Visit(exprs);
    if (collector.Dispatches) {
      return collector.DispatchDescription;
    }
    // Breadth-first search, through nodes that reach open dispatch, to one that makes it itself.
    var parent = new Dictionary<Declaration, Declaration?>();
    var queue = new Queue<Declaration>();
    foreach (var start in collector.Callees.Where(reaching.Contains).OrderBy(NodeName, StringComparer.Ordinal)) {
      parent[start] = null;
      queue.Enqueue(start);
    }
    while (queue.Count != 0) {
      var node = queue.Dequeue();
      if (directDispatch.TryGetValue(node, out var dispatch)) {
        var chain = new List<string> { dispatch };
        for (Declaration? n = node; n != null; n = parent[n]) {
          chain.Add(NodeName(n));
        }
        chain.Reverse();
        return string.Join(" -> ", chain);
      }
      foreach (var next in edges.GetValueOrDefault(node, []).Where(reaching.Contains).OrderBy(NodeName, StringComparer.Ordinal)) {
        if (parent.TryAdd(next, node)) {
          queue.Enqueue(next);
        }
      }
    }
    return null;
  }

  private static string NodeName(Declaration node) {
    return node switch {
      MemberDecl member => $"'{ModularTermination.QualifiedName(member)}'",
      IteratorDecl iter => $"iterator '{iter.EnclosingModuleDefinition.Name}.{iter.Name}'",
      TopLevelDecl type => $"type '{type.EnclosingModuleDefinition.Name}.{type.Name}'",
      _ => $"'{node.Name}'"
    };
  }

  private class EdgeCollector : BottomUpVisitor {
    private readonly ModularTerminationAnalysis analysis;
    public readonly HashSet<Declaration> Callees = [];
    // Every declaration the code depends on, in any module (issue 211)
    public readonly HashSet<Declaration> AllCallees = [];
    public bool AppliesArgumentBound;
    public bool Dispatches;
    public string? DispatchDescription;

    private void Dispatch(string description) {
      if (!Dispatches) {
        Dispatches = true;
        DispatchDescription = description;
      }
    }

    /// <summary>The member being visited has no body, and a refining module may give it one.</summary>
    public void BodyToCome() {
      Dispatch("no body, which a refining module may give it");
    }

    private readonly ModuleDefinition? callerModule;
    private readonly ICallable? owner;

    public EdgeCollector(ModularTerminationAnalysis analysis, ModuleDefinition? callerModule, ICallable? owner = null) {
      this.analysis = analysis;
      this.callerModule = callerModule;
      this.owner = owner;
    }

    private void AddDependency(Declaration node) {
      AllCallees.Add(node);
      if (callerModule != null && ModularTermination.IsOutside(node, callerModule)) {
        if (analysis.HasGeneralContract(node, callerModule)) {
          Dispatch($"General contract of {NodeName(node)} (publish {{:termination_closed}} for independent closed code)");
        }
      } else {
        Callees.Add(node);
      }
    }

    public void Mention(Type? type) {
      foreach (var decl in TypeMentions.Declarations(type)) {
        AddDependency(decl);
      }
    }

    public void MentionSignature(ICallable callable) {
      foreach (var type in TypeMentions.OfSignature(callable)) {
        Mention(type);
      }
    }

    private void Call(MemberDecl callee) {
      if (analysis.IsOpen(callee)) {
        Dispatch($"dispatch of '{ModularTermination.QualifiedName(callee)}'");
      } else {
        AddDependency(NodeOf(callee));
      }
    }

    protected override void VisitOneExpr(Expression expr) {
      foreach (var type in TypeMentions.Of(expr)) {
        Mention(type);
      }
      switch (expr) {
        // The orders library's Of(r) and WfoOf(r) make the order whose gt and aha run r and nothing else, and its
        // wellFounded(r) and stops(r, c) run only r and c: each is an application of its arguments of arrow type
        // (https://github.com/erniecohen/dafny/issues/245, M4).
        case FunctionCallExpr call when call.Function != null && analysis.AppliesOnlyItsArguments(call.Function):
          foreach (var arg in call.Args.Where(a => a.Type.NormalizeExpand().IsArrowType)) {
            Apply(arg);
          }
          break;
        case DatatypeValue value when value.Ctor != null && DecreasesOrders.IsLibraryWfoOf(value.Ctor.EnclosingDatatype):
          Apply(value.Arguments[0]);
          break;
        case FunctionCallExpr call when call.Function != null:
          if (ModularTermination.IsArrowMember(call.Function)) {
            Apply(call.Receiver);
          } else {
            Call(call.Function);
          }
          break;
        // A method is never a value, so selecting one of the orders library's multiset lemmas is the callee of a
        // call statement, which VisitOneStmt visits as an application of its arguments of arrow type.
        case MemberSelectExpr { Member: MethodOrConstructor lemma } when analysis.AppliesOnlyItsArguments(lemma):
          break;
        case MemberSelectExpr { Member: Function or MethodOrConstructor or ConstantField } select:
          Call(select.Member);
          break;
        case ApplyExpr apply:
          Apply(apply.Function);
          break;
        case SeqConstructionExpr seq:
          Apply(seq.Initializer);
          break;
        case DecreasesByExpr decreasesBy when DecreasesOrders.WfoTrait(decreasesBy.Order.Type) != null:
          // A component "e by O" is an implied call of Wfo.gt and Wfo.aha (R3 of DecreasesOrders) on the value of O.
          foreach (var implementation in analysis.orderImplementations) {
            Call(implementation);
          }
          break;
      }
    }

    /// <summary>
    /// An application of a function value (M3b).  A lambda's body is visited as part of the enclosing code, and a
    /// function named as a value is an edge already (a reference); any other value, including a parameter with a
    /// callback bound, runs code that is not known, which counts as open dispatch.
    /// </summary>
    private void Apply(Expression value) {
      // The checked summary has accounted for every use and escape of these local arrow values.
      if (owner is MemberDecl member && analysis.argumentSummaries.Contains(member) &&
          value.Resolved is IdentifierExpr) {
        return;
      }
      if (value.Resolved is LambdaExpr) {
        return;
      }
      if (value.Resolved is MemberSelectExpr { Member: Function g } && !ModularTermination.IsArrowMember(g)) {
        return;
      }
      if (value.Resolved is IdentifierExpr { Var: { } binder } && analysis.logicalBinders.Contains(binder)) {
        return;
      }
      // The argument-dependent bound of a bounded value was checked to be closed when the value was formed, and to
      // apply no such bound itself.
      if (BoundedCallables.IsArgumentBound(value)) {
        AppliesArgumentBound = true;
        return;
      }
      // The relation MultisetRel(r) of the orders library applies only r, which the visit of that call applies.
      if (value.Resolved is FunctionCallExpr { Function: { } function } && DecreasesOrders.IsLibraryMultisetRel(function)) {
        return;
      }
      Dispatch("a call through a function value whose code is not known");
    }

    protected override void VisitOneStmt(Statement stmt) {
      foreach (var type in TypeMentions.Of(stmt)) {
        Mention(type);
      }
      if (stmt is CallStmt { Method: { } method }) {
        if (analysis.AppliesOnlyItsArguments(method)) {
          // The orders library's lemmas of the multiset order (https://github.com/erniecohen/dafny/issues/211)
          foreach (var arg in ((CallStmt)stmt).Args.Where(a => a.Type.NormalizeExpand().IsArrowType)) {
            Apply(arg);
          }
        } else {
          Call(method);
        }
      }
      if (stmt is SingleAssignStmt { Rhs: AllocateArray { ElementInit: { } init } }) {
        Apply(init);
      }
    }
  }
}

/// <summary>
/// The types that a piece of code mentions, for --modular-termination: a value of such a type may be checked
/// against the type's constraint (a type test, a conversion, a subset check where a value is assigned, passed or
/// returned) or created from its witness or from the default values of its components.  The list is an
/// over-approximation of those places: it holds the type of every expression, the types that an expression or
/// statement names (bound variables, locals, type arguments, conversion and test targets, allocated types), and
/// the formal types of a datatype constructor that an expression applies.
/// </summary>
public static class TypeMentions {
  public static IEnumerable<Type> Of(Expression expr) {
    if (expr.Type != null) {
      yield return expr.Type;
    }
    foreach (var type in expr.ComponentTypes) {
      yield return type;
    }
    switch (expr) {
      case TypeUnaryExpr unary:
        yield return unary.ToType;
        break;
      case DatatypeValue { Ctor: { } ctor }:
        foreach (var formal in ctor.Formals) {
          yield return formal.Type;
        }
        break;
    }
  }

  public static IEnumerable<Type> Of(Statement stmt) {
    switch (stmt) {
      case VarDeclStmt varDecl:
        foreach (var local in varDecl.Locals) {
          yield return local.Type;
        }
        break;
      case VarDeclPattern pattern:
        foreach (var local in pattern.LocalVars) {
          yield return local.Type;
        }
        break;
      case ForallStmt forall:
        foreach (var bv in forall.BoundVars) {
          yield return bv.Type;
        }
        break;
      case ForLoopStmt forLoop:
        yield return forLoop.LoopIndex.Type;
        break;
      case SingleAssignStmt { Rhs: TypeRhs typeRhs }:
        yield return typeRhs.Type;
        if (typeRhs is AllocateArray array) {
          yield return array.ElementType;
        }
        break;
      case CallStmt call:
        foreach (var type in call.MethodSelect.ComponentTypes) {
          yield return type;
        }
        break;
    }
  }

  public static IEnumerable<Type> OfSignature(ICallable callable) {
    foreach (var formal in callable.Ins) {
      yield return formal.Type;
    }
    switch (callable) {
      case MethodOrConstructor method:
        foreach (var formal in method.Outs) {
          yield return formal.Type;
        }
        break;
      case Function function:
        yield return function.ResultType;
        break;
    }
  }

  /// <summary>
  /// The declarations of the subset types, newtypes, datatypes, classes and traits that "type" is built from.
  /// </summary>
  public static IEnumerable<TopLevelDecl> Declarations(Type? type) {
    if (type == null) {
      yield break;
    }
    var normalized = type.Normalize();
    if (normalized is UserDefinedType { ResolvedClass: { } decl }) {
      switch (decl) {
        case NonNullTypeDecl { Class: not IteratorDecl } nonNull:
          yield return nonNull.Class;
          break;
        case NonNullTypeDecl:
          break;
        case TypeSynonymDecl or NewtypeDecl or DatatypeDecl or AbstractTypeDecl or ClassLikeDecl and not IteratorDecl:
          yield return decl;
          break;
      }
    }
    foreach (var argument in normalized.TypeArgs) {
      foreach (var d in Declarations(argument)) {
        yield return d;
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

  internal override void PreResolve(Program program) {
    program.Visit((INode node) => {
      var attributes = node switch {
        IAttributeBearingDeclaration host => host.Attributes,
        Formal formal => formal.Attributes,
        _ => null
      };
      if (Attributes.Find(attributes, ModularTermination.ClosedAttributeName) is { } attribute &&
          (attribute.Args.Count != 0 || node is not
            (Function or MethodOrConstructor or ConstantField or TopLevelDeclWithMembers or TypeSynonymDecl))) {
        Reporter.Error(MessageSource.Resolver, attribute.Origin,
          "modular termination: {:termination_closed} takes no arguments and applies to a callable, constant, type or iterator");
      }
      return true;
    });
  }

  internal override void PreResolve(ModuleDefinition module) {
    foreach (var declaration in module.TopLevelDecls) {
      bool hasEvaluation = declaration switch {
        NonNullTypeDecl => false,
        TypeSynonymDecl or NewtypeDecl or IteratorDecl or AbstractTypeDecl => true,
        DatatypeDecl datatype => datatype.Ctors.Any(c => c.Formals.Count != 0),
        TopLevelDeclWithMembers type => type.Members.OfType<Field>().Any() || type.Traits.Count != 0,
        _ => false
      };
      declaration.TerminationContract ??= declaration.CardinalityRefinementBase?.TerminationContract ?? new TerminationSourceContract(
        $"{module.FullDafnyName}.{declaration.Name}", false, hasEvaluation,
        declaration is TraitDecl trait && ModularTermination.IsExtensible(trait));
      if (declaration is not TopLevelDeclWithMembers cl) {
        continue;
      }
      foreach (var member in cl.Members) {
        member.TerminationContract ??= member.RefinementBase?.TerminationContract ?? new TerminationSourceContract(
          $"{module.FullDafnyName}.{cl.Name}.{member.Name}",
          cl is TraitDecl && !member.HasStaticKeyword && (member is Method || member is Function { Body: null }),
          member is not ConstantField || member is ConstantField { Rhs: not null },
          cl is TraitDecl { IsObjectTrait: false });
      }
    }
  }

  /// <summary>
  /// By default, a library that Dafny does not verify here and that was not verified under the rules (a source file
  /// or a .dll given with --library, or a .doo built by a version of Dafny without them) is refused when it uses the
  /// design: its leaf and bound obligations were never checked (https://github.com/erniecohen/dafny/issues/211, §6).
  /// A library that uses none of the design is harmless: its code is TOP and no leaf.  Under --modular-termination
  /// the parser admits libraries by their records instead.
  /// </summary>
  private void CheckUncheckedLibraries(Program program) {
    var roots = program.Compilation.UncheckedTerminationRoots;
    if (roots.Count == 0) {
      return;
    }
    var edges = program.Compilation.Includes.GroupBy(i => i.IncluderFilename)
      .ToDictionary(g => g.Key, g => g.Select(i => i.IncludedFilename).ToList());
    var uncheckedFiles = new Dictionary<Uri, Uri>();
    foreach (var root in roots) {
      var work = new Stack<Uri>([root]);
      while (work.TryPop(out var uri)) {
        if (uncheckedFiles.TryAdd(uri, root)) {
          foreach (var included in edges.GetValueOrDefault(uri, [])) {
            work.Push(included);
          }
        }
      }
    }
    var reported = new HashSet<Uri>();
    while (ModularTermination.FirstDesignUse(program, uri => uncheckedFiles.ContainsKey(uri) && !reported.Contains(uncheckedFiles[uri]))
           is var (declaration, use)) {
      var root = uncheckedFiles[declaration.Origin.Uri];
      reported.Add(root);
      Reporter.Error(MessageSource.Resolver, "modular_termination_library", declaration.Origin,
        $"cannot use {Reporter.Options.GetPrintPath(root.LocalPath)} as a library: it uses {use} here, but it was not " +
        "verified under the rules of termination across modules; verify it with this program, or build it into a .doo " +
        "with this version of Dafny");
    }
  }

  internal override void PostResolve(Program program) {
    if (ModularTermination.RulesEnabled(Reporter.Options)) {
      CheckUncheckedLibraries(program);
    }
    if (!ModularTermination.Active(Reporter.Options, program)) {
      return;
    }
    CheckLibraryRefinements(program);
    var analysis = new ModularTerminationAnalysis(program);
    var checkedClauses = new HashSet<(Attributes, ModuleDefinition)>();
    foreach (var module in program.Modules()) {
      foreach (var decl in module.TopLevelDecls) {
        CheckClosedPromise(decl, analysis);
        if (decl is TopLevelDeclWithMembers type) {
          CheckExtensibleAncestors(type);
          foreach (var member in type.Members) {
            CheckClosedPromise(member, analysis);
            CheckForeignImplementation(member, analysis);
          }
        }
        if (decl is not TopLevelDeclWithMembers cl) {
          continue;
        }
        foreach (var member in cl.Members) {
          if (member is not (MethodOrConstructor or Function) || !member.ShouldVerify(program.Compilation)) {
            continue;
          }
          var callable = (ICallable)member;
          CheckGlobalClauses(callable);
          CheckCallbackClauses(callable);
          CheckBoundsAreClosed(callable, analysis, checkedClauses);
          var classifier = new CallClassifier(Reporter, callable, analysis);
          classifier.Prepare(CallableExpressions(callable), CallableStatements(callable));
          classifier.VisitSignature();
          classifier.Visit(callable);
          classifier.Visit(callable.Ins.Where(f => f.DefaultValue != null).Select(f => f.DefaultValue));
        }
      }
    }
  }

  /// <summary>
  /// A module that refines or replaces a module of a library inherits that module's bodies, which are
  /// not part of the library's termination contracts (https://github.com/erniecohen/dafny/issues/211,
  /// milestone M3-4): a changed body would leave the contract, and so the record of a library built
  /// against it, unchanged.  Until contracts or records cover inherited bodies, such a module is rejected.
  /// </summary>
  private void CheckLibraryRefinements(Program program) {
    var urisToVerify = program.Compilation.UrisToVerify;
    if (urisToVerify == null) {
      return;
    }
    foreach (var module in program.Modules()) {
      if (module.Implements is not { } implements || implements.Target.Def is not { } target ||
          module.Origin?.Uri == null || !urisToVerify.Contains(module.Origin.Uri) ||
          target.Origin?.Uri == null || urisToVerify.Contains(target.Origin.Uri)) {
        continue;
      }
      var verb = implements.Kind == ImplementationKind.Refinement ? "refines" : "replaces";
      Reporter.Error(MessageSource.Resolver, module.NameNode.Origin,
        $"modular termination: module '{module.Name}' {verb} module '{target.FullDafnyName}' of a library; " +
        "under --modular-termination a module cannot refine or replace a library module, since the bodies it " +
        "would inherit are not part of the library's termination contracts");
    }
  }

  private void CheckExtensibleAncestors(TopLevelDeclWithMembers child) {
    if (child is TraitDecl refined && child.TerminationContract is { } contract &&
        contract.IsExtensibleOwner != ModularTermination.IsExtensible(refined)) {
      Reporter.Error(MessageSource.Resolver, child.Origin,
        "modular termination: refinement cannot change a trait's declared extensibility");
    }
    // Every trait may be extended from any module (rule 1 of revision 3), so no ancestor is sealed.
  }

  /// <summary>
  /// Rule 3 of revision 3: a foreign implementation of a member with no declared bound must be a leaf, unless it
  /// has a bound of its own, declared or inherited from a subtrait.  A declared bound is checked like any other: every call the implementation makes
  /// must be below it (or, to known code that reaches open dispatch, at most it), so the implementation cannot reach
  /// a call dispatched to a member without a bound, which is TOP.  The order of bounds has no bottom, and needs none:
  /// what matters is that bounded code never reaches TOP, so no cycle through an unbounded dispatch can close.
  /// </summary>
  private void CheckForeignImplementation(MemberDecl member, ModularTerminationAnalysis analysis) {
    if (member is not (MethodOrConstructor or Function) || ModularTermination.UndeclaredForeignAncestor(member) is not { } ancestor) {
      return;
    }
    if (ModularTermination.IsDispatchSlot(member) && member is Method { Body: null } or Function { Body: null }) {
      return; // a bodyless member of a subtrait: its own implementations are checked
    }
    var name = ModularTermination.QualifiedName(ancestor);
    if (ModularTermination.HasGlobalBound((ICallable)member)) {
      return; // its bound, declared or inherited from a subtrait, is checked at each call it makes
    }
    if (analysis.NonLeafChain(member) is { } chain) {
      Reporter.Error(MessageSource.Resolver, member.Origin,
        $"modular termination: '{member.Name}' implements '{name}' of another module, which has no 'decreases {{:global}}' bound, " +
        $"so it must be a leaf or declare a bound, but it is not a leaf ({chain}); declare a bound on '{name}' or on this implementation, or make this implementation a leaf");
    }
  }

  private void CheckClosedPromise(Declaration declaration, ModularTerminationAnalysis analysis) {
    if (!ModularTermination.HasClosedContract(declaration)) {
      return;
    }
    if (declaration is MemberDecl member && ModularTermination.IsOpenDispatch(member, analysis.OpenTraits)) {
      Reporter.Error(MessageSource.Resolver, declaration.Origin,
        "modular termination: a trait's overridable slot cannot promise {:termination_closed}; use a global bound");
    }
    if (declaration is ICallable callable && ModularTermination.HasCallbackBounds(callable)) {
      Reporter.Error(MessageSource.Resolver, declaration.Origin,
        "modular termination: {:termination_closed} cannot be combined with callback bounds");
    }
    if (analysis.ReachingDeclaration(declaration)) {
      Reporter.Error(MessageSource.Resolver, declaration.Origin,
        $"modular termination: the {{:termination_closed}} promise of '{declaration.Name}' is not established; " +
        "its body, specifications or implicit evaluations can reach open dispatch, an unknown application or an imported General contract");
    }
  }

  /// <summary>
  /// The expressions and statements that <see cref="BottomUpVisitor.Visit(ICallable)"/> visits, and the default
  /// values of the in-parameters.
  /// </summary>
  private static IEnumerable<Expression> CallableExpressions(ICallable callable) {
    var exprs = new List<Expression>();
    switch (callable) {
      case Function f:
        exprs.AddRange(f.Req.Select(e => e.E));
        exprs.AddRange(f.Reads.Expressions.Select(e => e.E));
        exprs.AddRange(f.Ens.Select(e => e.E));
        exprs.AddRange(f.Decreases.Expressions);
        if (f.Body != null) {
          exprs.Add(f.Body);
        }
        break;
      case MethodOrConstructor m:
        exprs.AddRange(m.Req.Select(e => e.E));
        exprs.AddRange(m.Reads.Expressions.Select(e => e.E));
        exprs.AddRange(m.Mod.Expressions.Select(e => e.E));
        exprs.AddRange(m.Ens.Select(e => e.E));
        exprs.AddRange(m.Decreases.Expressions);
        break;
    }
    exprs.AddRange(callable.Ins.Where(f => f.DefaultValue != null).Select(f => f.DefaultValue));
    return exprs;
  }

  private static IEnumerable<Statement> CallableStatements(ICallable callable) {
    return callable switch {
      Function { ByMethodBody: { } body } => [body],
      MethodOrConstructor { Body: { } body } => [body],
      _ => []
    };
  }

  /// <summary>
  /// M4a: a global or callback bound may not reach open dispatch.  Its value is the measure by which the bound
  /// obligations establish the termination of the code that reaches open dispatch, so it must not be computed by
  /// that code: otherwise an obligation can be proved from the definition of the very function whose termination it
  /// establishes.  The bounds checked are those that "callable" declares and those that it inherits, which the
  /// verifier evaluates in its own proof obligations even when the member that declares them is not verified.
  /// </summary>
  private void CheckBoundsAreClosed(ICallable callable, ModularTerminationAnalysis analysis, HashSet<(Attributes, ModuleDefinition)> checkedClauses) {
    for (ICallable? c = callable; c != null; c = (c as MemberDecl)?.OverriddenMember as ICallable) {
      if (c is not (MethodOrConstructor or Function)) {
        break;
      }
      foreach (var clause in ModularTermination.GlobalClauses(c.Decreases.Attributes)) {
        CheckBoundIsClosed(c, clause, clause.Args, "'decreases {:global}' bound", analysis, checkedClauses, callable.EnclosingModule);
      }
      foreach (var clause in ModularTermination.CallbackClauses(c.Decreases.Attributes)) {
        CheckBoundIsClosed(c, clause, clause.Args.Skip(1), "'decreases {:callback}' bound", analysis, checkedClauses, callable.EnclosingModule);
      }
    }
  }

  private void CheckBoundIsClosed(ICallable declarer, Attributes clause, IEnumerable<Expression> bound, string what,
                                  ModularTerminationAnalysis analysis, HashSet<(Attributes, ModuleDefinition)> checkedClauses, ModuleDefinition callerModule) {
    if (!checkedClauses.Add((clause, callerModule)) || analysis.ReachingChain(bound, callerModule) is not { } chain) {
      return;
    }
    Reporter.Error(MessageSource.Resolver, clause.Origin,
      $"modular termination: the {what} of '{ModularTermination.QualifiedName((MemberDecl)declarer)}' can reach dispatch to " +
      $"implementations that other modules may provide (bound -> {chain}); a bound may only use code that cannot reach one, " +
      "since its value must not depend on the termination that the bounds establish");
  }

  private void CheckGlobalClauses(ICallable callable) {
    var clauses = ModularTermination.GlobalClauses(callable.Decreases.Attributes).ToList();
    if (clauses.Count > 1) {
      Reporter.Error(MessageSource.Resolver, clauses[0].Origin,
        $"{callable.WhatKind} '{callable.NameRelativeToModule}' has more than one 'decreases {{:global}}' clause");
    }
  }

  /// <summary>
  /// A callback clause names an in-parameter of the callable, of an arrow type, at most once, and is declared by a
  /// member that overrides nothing: an override has the callback bounds of the member it overrides.
  /// </summary>
  private void CheckCallbackClauses(ICallable callable) {
    var named = new HashSet<Formal>();
    foreach (var clause in ModularTermination.CallbackClauses(callable.Decreases.Attributes)) {
      var formal = ModularTermination.CallbackParameter(callable, clause);
      if (formal == null || !formal.Type.IsArrowType) {
        Reporter.Error(MessageSource.Resolver, clause.Args[0].Origin,
          "the first argument of a 'decreases {:callback f}' clause must be an in-parameter of the method or function, of an arrow type");
      } else if (!named.Add(formal)) {
        Reporter.Error(MessageSource.Resolver, clause.Origin,
          $"in-parameter '{formal.Name}' has more than one 'decreases {{:callback}}' clause");
      } else if (callable is MemberDecl { OverriddenMember: not null }) {
        Reporter.Error(MessageSource.Resolver, clause.Origin,
          "an overriding member cannot declare a 'decreases {:callback}' clause: it has the callback bounds of the member it overrides");
      }
    }
  }

  private class CallClassifier : BottomUpVisitor {
    private readonly ErrorReporter reporter;
    private readonly ICallable caller;
    private readonly ModularTerminationAnalysis analysis;
    // A call can be reached more than once, for instance through a quantifier's triggers; it is
    // reported once, at its position.
    private readonly HashSet<(System.Uri, int, int, bool)> reported = new();
    // A type is reported once per line: a declaration, its expressions and their subexpressions mention the same type.
    private readonly HashSet<(System.Uri, int, TopLevelDecl)> reportedTypes = new();

    // The code inside a lambda expression passed for a parameter with a callback bound (a bounded lambda, M3b),
    // whose body runs under that bound, and so does every lambda inside it.
    private readonly HashSet<object> insideBoundedLambda = new(ReferenceEqualityComparer.Instance);
    // The member selections that a call statement names its method by.
    private readonly HashSet<object> calledSelections = new(ReferenceEqualityComparer.Instance);

    public CallClassifier(ErrorReporter reporter, ICallable caller, ModularTerminationAnalysis analysis) {
      this.reporter = reporter;
      this.caller = caller;
      this.analysis = analysis;
    }

    public void VisitSignature() {
      ReportTypes(((MemberDecl)caller).Origin, TypeMentions.OfSignature(caller), ModularTermination.HasGlobalBound(caller));
    }

    /// <summary>
    /// Records which code lies inside lambda expressions, before the visit.
    /// </summary>
    public void Prepare(IEnumerable<Expression> exprs, IEnumerable<Statement> stmts) {
      foreach (var e in exprs) {
        Walk(e, false);
      }
      foreach (var st in stmts) {
        Walk(st, false);
      }
    }

    private void Walk(object node, bool inBounded) {
      if (inBounded) {
        insideBoundedLambda.Add(node);
      }
      var bounded = new HashSet<object>(ReferenceEqualityComparer.Instance);
      switch (node) {
        case FunctionCallExpr { Function: { } f } call:
          foreach (var arg in BoundedArguments(f, call.Args)) {
            bounded.Add(arg);
          }
          break;
        case CallStmt { Method: { } m } callStmt:
          calledSelections.Add(callStmt.MethodSelect);
          foreach (var arg in BoundedArguments(m, callStmt.Args)) {
            bounded.Add(arg);
          }
          break;
      }
      switch (node) {
        case Expression expr:
          foreach (var sub in expr.SubExpressions) {
            var isLambda = sub.Resolved is LambdaExpr;
            Walk(sub, inBounded || (isLambda && bounded.Contains(sub)));
          }
          if (expr is StmtExpr stmtExpr) {
            Walk(stmtExpr.S, inBounded);
          }
          break;
        case Statement stmt:
          foreach (var sub in stmt.SubExpressions) {
            var isLambda = sub.Resolved is LambdaExpr;
            Walk(sub, inBounded || (isLambda && bounded.Contains(sub)));
          }
          foreach (var sub in stmt.SubStatements) {
            Walk(sub, inBounded);
          }
          break;
      }
    }

    /// <summary>
    /// The arguments that a call of "callee" passes for in-parameters that have a callback bound.
    /// </summary>
    private static IEnumerable<Expression> BoundedArguments(MemberDecl callee, List<Expression> args) {
      if (callee is not ICallable callable) {
        yield break;
      }
      for (var i = 0; i < args.Count && i < callable.Ins.Count; i++) {
        if (ModularTermination.HasCallbackBound(callable, i)) {
          yield return args[i];
        }
      }
    }

    /// <summary>
    /// Whether the code at "node" runs under a global bound: the caller's, or that of a bounded lambda around it.
    /// </summary>
    private bool Bounded(object node) {
      return ModularTermination.HasGlobalBound(caller) || insideBoundedLambda.Contains(node);
    }

    protected override void VisitOneExpr(Expression expr) {
      ReportTypes(expr.Origin, TypeMentions.Of(expr), Bounded(expr));
      switch (expr) {
        case MemberSelectExpr { Member: ConstantField constant } select
          when analysis.ReachesOpenDispatch(constant, caller.EnclosingModule):
          ReportTop(select.Origin, $"the initializer of constant '{QualifiedName(constant)}'", Bounded(expr));
          break;
        case MemberSelectExpr { Member: Function f } select when !calledSelections.Contains(select) &&
                                                               ModularTermination.HasCallbackBounds(f):
          reporter.Error(MessageSource.Resolver, select.Origin,
            $"modular termination: '{QualifiedName(f)}' has a parameter with a 'decreases {{:callback}}' bound, so it cannot be used as a function value");
          break;
        case FunctionCallExpr { Function: { } f } call when ModularTermination.IsArrowMember(f):
          ReportApplication(call.Origin, call.Receiver, expr, $"a call of '{f.Name}' of a function value", false);
          break;
        case FunctionCallExpr call:
          ReportDispatch(call.Origin, call.Function);
          ReportBoundedArguments(call.Function, call.Args, expr);
          break;
        case ApplyExpr apply:
          ReportApplication(apply.Origin, apply.Function, expr, "a call through a function value", false);
          break;
        case SeqConstructionExpr seq:
          ReportApplication(seq.Origin, seq.Initializer, expr, "a sequence constructor's calls of its initializing function", true);
          break;
      }
    }

    protected override void VisitOneStmt(Statement stmt) {
      ReportTypes(stmt.Origin, TypeMentions.Of(stmt), Bounded(stmt));
      switch (stmt) {
        case CallStmt call:
          ReportDispatch(call.Origin, call.Method);
          ReportBoundedArguments(call.Method, call.Args, stmt);
          break;
        case SingleAssignStmt { Rhs: AllocateArray { ElementInit: { } init } } assign:
          ReportApplication(assign.Origin, init, stmt, "an array allocation's calls of its initializing function", true);
          break;
      }
    }

    /// <summary>
    /// An application of a function value (M3b).  A lambda's body is checked where it appears, and a function
    /// named as a value that reaches no dispatched call needs no check.  A parameter with a callback bound runs
    /// under that bound, which the verifier checks to be below the caller's, or inside a lambda the lambda's
    /// (smaller-bound rule), except as the initializer of a sequence or an array, where it counts as unknown.  Any
    /// other value runs code that is not known, which has no bound (TOP): an error where the code runs under a bound.
    /// </summary>
    private void ReportApplication(IOrigin origin, Expression value, object site, string what, bool initializer) {
      var kind = analysis.Classify(value, caller);
      if (kind == ModularTerminationAnalysis.FunctionValueKind.CallbackParameter && initializer ||
          kind == ModularTerminationAnalysis.FunctionValueKind.Bounded && initializer) {
        kind = ModularTerminationAnalysis.FunctionValueKind.Unknown;
      }
      if (kind == ModularTerminationAnalysis.FunctionValueKind.Lambda || !FirstReport(origin)) {
        return;
      }
      var bounded = Bounded(site);
      switch (kind) {
        case ModularTerminationAnalysis.FunctionValueKind.KnownCode:
          reporter.Info(MessageSource.Resolver, origin,
            $"modular termination: in {CallerName}, {what} needs the smaller-bound rule; holds: the value is known code that reaches no dispatched call");
          break;
        case ModularTerminationAnalysis.FunctionValueKind.Binder:
          reporter.Info(MessageSource.Resolver, origin,
            $"modular termination: in {CallerName}, {what} needs the smaller-bound rule; holds: the value is a variable bound by a quantifier, which ranges over functions that exist already, so applying it calls nothing");
          break;
        case ModularTerminationAnalysis.FunctionValueKind.Bounded:
          reporter.Info(MessageSource.Resolver, origin,
            $"modular termination: in {CallerName}, {what} needs the smaller-bound rule; " +
            (bounded ? "checked: the bounded value's bound must be below the caller's" : "holds: the caller has no 'decreases {:global}' bound, which is above every bound"));
          break;
        case ModularTerminationAnalysis.FunctionValueKind.CallbackParameter:
          reporter.Info(MessageSource.Resolver, origin,
            $"modular termination: in {CallerName}, {what} needs the smaller-bound rule; " +
            (bounded ? "checked: the parameter's 'decreases {:callback}' bound must be below the caller's" : "holds: the caller has no 'decreases {:global}' bound, which is above every bound"));
          break;
        default:
          if (bounded) {
            reporter.Error(MessageSource.Resolver, origin,
              $"modular termination: in {CallerName}, {what} needs the smaller-bound rule; rejected: the value's code is not known, so it has no bound, " +
              "and the caller has one (pass the value for a parameter with a 'decreases {:callback}' bound)");
          } else if (ModularTermination.ImplementsOpenDispatch((MemberDecl)caller, analysis.OpenTraits)) {
            reporter.Info(MessageSource.Resolver, origin,
              $"modular termination: in {CallerName}, {what} needs the smaller-bound rule; holds until the trait member that the caller implements gets a 'decreases {{:global}}' bound, as its dispatched calls require; the value's code is not known");
          } else {
            reporter.Info(MessageSource.Resolver, origin,
              $"modular termination: in {CallerName}, {what} needs the smaller-bound rule; holds: the caller has no 'decreases {{:global}}' bound, which is above every bound; the value's code is not known");
          }
          break;
      }
    }

    /// <summary>
    /// A value passed for a parameter with a callback bound must run under that bound: a lambda expression, whose
    /// body the verifier checks under it; a parameter of the caller with a callback bound, which the verifier checks
    /// to be at most the callee's (outside a lambda); or a function named as a value that reaches no dispatched call.
    /// </summary>
    private void ReportBoundedArguments(MemberDecl callee, List<Expression> args, object site) {
      if (callee is not ICallable callable) {
        return;
      }
      if (BoundedCallables.IsCertify(callee) && args.Count == 2 && BoundedCallables.ArgumentOrderResult(args[0].Type) != null) {
        ReportArgumentDependentCertify(callee, args[0], args[1]);
        return;
      }
      for (var i = 0; i < args.Count && i < callable.Ins.Count; i++) {
        if (!ModularTermination.HasCallbackBound(callable, i)) {
          continue;
        }
        var kind = analysis.Classify(args[i], caller);
        if (kind is ModularTerminationAnalysis.FunctionValueKind.Bounded &&
            BoundedCallables.ArgumentOrderResult(BoundedCallables.ErasedCallable(args[i])!.Type.NormalizeExpand().TypeArgs[0]) != null) {
          reporter.Error(MessageSource.Resolver, args[i].Origin,
            $"modular termination: in {CallerName}, the function of a bounded value with an argument-dependent bound cannot be passed for " +
            $"parameter '{callable.Ins[i].Name}' of '{QualifiedName(callee)}', whose 'decreases {{:callback}}' bound does not depend on the arguments; pass the bounded value itself");
          continue;
        }
        if (kind is ModularTerminationAnalysis.FunctionValueKind.Unknown && BoundedCallables.IsCertify(callee)) {
          if (ModularTermination.AdmittedMember(reporter.Options, args[i], out var problem) != null) {
            reporter.Info(MessageSource.Resolver, args[i].Origin,
              $"modular termination: in {CallerName}, the function certified by '{QualifiedName(callee)}' runs its own code; " +
              "checked: its 'decreases {:global}' bound must not exceed the bound given");
            continue;
          }
          if (problem != null) {
            reporter.Error(MessageSource.Resolver, args[i].Origin,
              $"modular termination: in {CallerName}, the function passed to '{QualifiedName(callee)}' cannot be certified: {problem}");
            continue;
          }
        }
        if (kind is ModularTerminationAnalysis.FunctionValueKind.Unknown or ModularTerminationAnalysis.FunctionValueKind.Binder) {
          reporter.Error(MessageSource.Resolver, args[i].Origin,
            $"modular termination: in {CallerName}, the value passed for parameter '{callable.Ins[i].Name}' of '{QualifiedName(callee)}', " +
            "which has a 'decreases {:callback}' bound, must be a lambda expression, a parameter with such a bound, " +
            "a function that reaches no dispatched call, or the function of a bounded value");
        }
      }
    }

    /// <summary>
    /// "bounded.certify(b, f)" with an argument-dependent bound b (issue 211): f must be a lambda expression, whose
    /// body the verifier checks under b at its arguments, and b must be closed (rule 8), since "X.bound(args)" is
    /// evaluated in the bounds of later calls: a lambda expression, a call of a function whose body is one, or a
    /// function, that reaches no dispatch or unknown application and applies no argument-dependent bound, or the
    /// argument-dependent bound of another bounded value.
    /// </summary>
    private void ReportArgumentDependentCertify(MemberDecl callee, Expression b, Expression f) {
      if (f.Resolved is not LambdaExpr) {
        reporter.Error(MessageSource.Resolver, f.Origin,
          $"modular termination: in {CallerName}, the function certified by '{QualifiedName(callee)}' at an argument-dependent bound must be a lambda expression");
      }
      // A call of a function whose body is a lambda expression is that lambda expression, with the call's arguments
      // for the function's parameters: it is a name for a family of bounds, such as the levels of the objects of
      // each depth, so that a specification can say which bound a value has.
      var closedCode = b.Resolved is LambdaExpr || BoundedCallables.IsArgumentBound(b) ||
                       b.Resolved is FunctionCallExpr { Function: { Body.Resolved: LambdaExpr } factory } && !factory.IsVirtual ||
                       analysis.Classify(b, caller) == ModularTerminationAnalysis.FunctionValueKind.KnownCode;
      if (!closedCode) {
        reporter.Error(MessageSource.Resolver, b.Origin,
          $"modular termination: in {CallerName}, the argument-dependent bound given to '{QualifiedName(callee)}' must be a lambda expression, " +
          "a call of a function whose body is one, a function that reaches no dispatched call, or the argument-dependent bound of a bounded value");
      } else if (analysis.ReachingChain([b], caller.EnclosingModule) is { } chain) {
        reporter.Error(MessageSource.Resolver, b.Origin,
          $"modular termination: in {CallerName}, the argument-dependent bound given to '{QualifiedName(callee)}' can reach dispatch to " +
          $"implementations that other modules may provide (bound -> {chain}); a bound may only use code that cannot reach one, " +
          "since its value must not depend on the termination that the bounds establish");
      } else if (analysis.AppliesArgumentBound([b])) {
        reporter.Error(MessageSource.Resolver, b.Origin,
          $"modular termination: in {CallerName}, the argument-dependent bound given to '{QualifiedName(callee)}' may apply the " +
          "argument-dependent bound of a bounded value, which could be itself; it must compute from data alone");
      }
    }

    private bool FirstReport(IOrigin origin, bool top = false) {
      var center = origin.Center;
      return reported.Add((center.Uri, center.line, center.col, top));
    }

    private void ReportDispatch(IOrigin origin, MemberDecl callee) {
      if (ModularTermination.IsOutside(callee, caller.EnclosingModule) && callee is ICallable callable) {
        for (ICallable? current = callable; current is MemberDecl member; current = member.OverriddenMember as ICallable) {
          var bounds = ModularTermination.GlobalClauses(current.Decreases.Attributes).SelectMany(a => a.Args)
            .Concat(ModularTermination.CallbackClauses(current.Decreases.Attributes).SelectMany(a => a.Args.Skip(1)));
          if (analysis.ReachingChain(bounds, caller.EnclosingModule) is { } chain) {
            reporter.Error(MessageSource.Resolver, origin,
              $"modular termination: the imported bound of '{QualifiedName(callee)}' requires independently closed public contracts (bound -> {chain})");
            break;
          }
        }
      }
      var dispatched = ModularTermination.IsOpenDispatch(callee, analysis.OpenTraits);
      if (!dispatched && (analysis.AppliesOnlyItsArguments(callee) ||
                          !analysis.ReachesOpenDispatch(callee, caller.EnclosingModule))) {
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

    /// <summary>
    /// A type that the code mentions may have its constraint, its witness, or the default values of its components
    /// evaluated there.  They belong to no method or function and have no global bound, so they are code without a
    /// bound (TOP): under the same-bound rule, they cannot run under the bound of a caller that has one if they
    /// can reach open dispatch.
    /// </summary>
    private void ReportTypes(IOrigin origin, IEnumerable<Type> types, bool bounded) {
      if (analysis.ReachingType(types, caller.EnclosingModule) is { } decl && reportedTypes.Add((origin.Center.Uri, origin.Center.line, decl))) {
        var what = decl is SubsetTypeDecl or NewtypeDecl
          ? $"the constraint or witness of type '{decl.EnclosingModuleDefinition.Name}.{decl.Name}'"
          : $"the default values of the components of type '{decl.EnclosingModuleDefinition.Name}.{decl.Name}'";
        ReportTop(origin, what, bounded);
      }
    }

    /// <summary>
    /// Code without a global bound that can reach open dispatch, and that "origin" may evaluate, runs under the
    /// caller's bound: an error if the caller has a bound, which TOP exceeds, and a hint otherwise.
    /// </summary>
    private void ReportTop(IOrigin origin, string what, bool bounded) {
      if (!FirstReport(origin, true)) {
        return;
      }
      if (bounded) {
        reporter.Error(MessageSource.Resolver, origin,
          $"modular termination: in {CallerName}, this may evaluate {what}, which can reach dispatch to implementations " +
          $"that other modules may provide and has no 'decreases {{:global}}' bound, so it cannot run under the caller's bound (same-bound rule)");
      } else {
        reporter.Info(MessageSource.Resolver, origin,
          $"modular termination: in {CallerName}, this may evaluate {what}, which can reach dispatch to implementations " +
          $"that other modules may provide and has no 'decreases {{:global}}' bound; holds: the caller has none, which is above every bound");
      }
    }

    private string CallerName => $"'{QualifiedName((MemberDecl)caller)}'";

    private static string QualifiedName(MemberDecl member) => ModularTermination.QualifiedName(member);
  }
}
