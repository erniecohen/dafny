#nullable enable
using System;
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
  // The callees of each node, and, for a node that makes an open dispatch itself, a description of the first one.
  private readonly Dictionary<Declaration, HashSet<Declaration>> edges = new();
  private readonly Dictionary<Declaration, string> directDispatch = new();

  /// <summary>
  /// The analysis of the program, computed once, the first time it is asked for.  Use it only once the
  /// program is fully resolved.
  /// </summary>
  public static ModularTerminationAnalysis Of(Program program) {
    return Cache.GetValue(program, p => new ModularTerminationAnalysis(p));
  }

  public ModularTerminationAnalysis(Program program) {
    OpenTraits = ModularTermination.OpenTraits(program);
    var callers = new Dictionary<Declaration, List<Declaration>>();
    var work = new Stack<Declaration>();
    void AddNode(Declaration node, System.Action<EdgeCollector> visit) {
      var collector = new EdgeCollector(this);
      visit(collector);
      if (collector.Dispatches) {
        directDispatch[node] = collector.DispatchDescription!;
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

  public enum FunctionValueKind {
    /// <summary>A lambda expression: its body is part of the code that creates it, and is checked there.</summary>
    Lambda,
    /// <summary>A function or method named as a value that reaches no open dispatch.</summary>
    KnownCode,
    /// <summary>An in-parameter of the enclosing callable that has a callback bound (M3b).</summary>
    CallbackParameter,
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
      case IdentifierExpr { Var: Formal f } when enclosing != null && enclosing.Ins.IndexOf(f) is var i and >= 0 &&
                                                  ModularTermination.HasCallbackBound(enclosing, i):
        return FunctionValueKind.CallbackParameter;
      case MemberSelectExpr { Member: Function g } when !ModularTermination.IsArrowMember(g) && !IsOpen(g) &&
                                                         !ReachesOpenDispatch(g) && !ModularTermination.HasCallbackBounds(g):
        return FunctionValueKind.KnownCode;
      default:
        return FunctionValueKind.Unknown;
    }
  }

  /// <summary>
  /// The first type declaration among "types" whose constraint, witness or components' default values may lead to
  /// open dispatch, or null.
  /// </summary>
  public TopLevelDecl? ReachingType(IEnumerable<Type> types) {
    return types.SelectMany(TypeMentions.Declarations).FirstOrDefault(d => reaching.Contains(d));
  }

  /// <summary>
  /// M4a: if the expressions "exprs" (a global or callback bound) can reach open dispatch, the shortest chain of
  /// code by which they do, ending in the dispatch itself, for instance "'Z.R' -> dispatch of 'Tr.T.F'"; else null.
  /// </summary>
  public string? ReachingChain(IEnumerable<Expression> exprs) {
    var collector = new EdgeCollector(this);
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
    public bool Dispatches;
    public string? DispatchDescription;

    private void Dispatch(string description) {
      if (!Dispatches) {
        Dispatches = true;
        DispatchDescription = description;
      }
    }

    public EdgeCollector(ModularTerminationAnalysis analysis) {
      this.analysis = analysis;
    }

    public void Mention(Type? type) {
      foreach (var decl in TypeMentions.Declarations(type)) {
        Callees.Add(decl);
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
        Callees.Add(NodeOf(callee));
      }
    }

    protected override void VisitOneExpr(Expression expr) {
      foreach (var type in TypeMentions.Of(expr)) {
        Mention(type);
      }
      switch (expr) {
        case FunctionCallExpr call when call.Function != null:
          if (ModularTermination.IsArrowMember(call.Function)) {
            Apply(call.Receiver);
          } else {
            Call(call.Function);
          }
          break;
        case MemberSelectExpr { Member: Function or MethodOrConstructor or ConstantField { Rhs: not null } } select:
          Call(select.Member);
          break;
        case ApplyExpr apply:
          Apply(apply.Function);
          break;
        case SeqConstructionExpr seq:
          Apply(seq.Initializer);
          break;
      }
    }

    /// <summary>
    /// An application of a function value (M3b).  A lambda's body is visited as part of the enclosing code, and a
    /// function named as a value is an edge already (a reference); any other value, including a parameter with a
    /// callback bound, runs code that is not known, which counts as open dispatch.
    /// </summary>
    private void Apply(Expression value) {
      if (value.Resolved is LambdaExpr) {
        return;
      }
      if (value.Resolved is MemberSelectExpr { Member: Function g } && !ModularTermination.IsArrowMember(g)) {
        return;
      }
      Dispatch("a call through a function value whose code is not known");
    }

    protected override void VisitOneStmt(Statement stmt) {
      foreach (var type in TypeMentions.Of(stmt)) {
        Mention(type);
      }
      if (stmt is CallStmt { Method: { } method }) {
        Call(method);
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
    var normalized = type.NormalizeExpandKeepConstraints();
    if (normalized is UserDefinedType { ResolvedClass: { } decl }) {
      switch (decl) {
        case NonNullTypeDecl { Class: not IteratorDecl } nonNull:
          yield return nonNull.Class;
          break;
        case NonNullTypeDecl:
          break;
        case SubsetTypeDecl or NewtypeDecl or DatatypeDecl or ClassLikeDecl and not IteratorDecl:
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

  internal override void PostResolve(Program program) {
    var analysis = new ModularTerminationAnalysis(program);
    var checkedClauses = new HashSet<Attributes>(ReferenceEqualityComparer.Instance);
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
  private void CheckBoundsAreClosed(ICallable callable, ModularTerminationAnalysis analysis, HashSet<Attributes> checkedClauses) {
    for (ICallable? c = callable; c != null; c = (c as MemberDecl)?.OverriddenMember as ICallable) {
      if (c is not (MethodOrConstructor or Function)) {
        break;
      }
      foreach (var clause in ModularTermination.GlobalClauses(c.Decreases.Attributes)) {
        CheckBoundIsClosed(c, clause, clause.Args, "'decreases {:global}' bound", analysis, checkedClauses);
      }
      foreach (var clause in ModularTermination.CallbackClauses(c.Decreases.Attributes)) {
        CheckBoundIsClosed(c, clause, clause.Args.Skip(1), "'decreases {:callback}' bound", analysis, checkedClauses);
      }
    }
  }

  private void CheckBoundIsClosed(ICallable declarer, Attributes clause, IEnumerable<Expression> bound, string what,
                                  ModularTerminationAnalysis analysis, HashSet<Attributes> checkedClauses) {
    if (!checkedClauses.Add(clause) || analysis.ReachingChain(bound) is not { } chain) {
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

    // The code inside a lambda expression, and inside a lambda expression passed for a parameter with a callback
    // bound (a bounded lambda, M3b), whose body runs under that bound, and so does every lambda inside it.
    private readonly HashSet<object> insideLambda = new(ReferenceEqualityComparer.Instance);
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
        Walk(e, false, false);
      }
      foreach (var st in stmts) {
        Walk(st, false, false);
      }
    }

    private void Walk(object node, bool inLambda, bool inBounded) {
      if (inLambda) {
        insideLambda.Add(node);
      }
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
            Walk(sub, inLambda || isLambda, inBounded || (isLambda && bounded.Contains(sub)));
          }
          if (expr is StmtExpr stmtExpr) {
            Walk(stmtExpr.S, inLambda, inBounded);
          }
          break;
        case Statement stmt:
          foreach (var sub in stmt.SubExpressions) {
            var isLambda = sub.Resolved is LambdaExpr;
            Walk(sub, inLambda || isLambda, inBounded || (isLambda && bounded.Contains(sub)));
          }
          foreach (var sub in stmt.SubStatements) {
            Walk(sub, inLambda, inBounded);
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
        case MemberSelectExpr { Member: ConstantField { Rhs: not null } constant } select
          when analysis.ReachesOpenDispatch(constant):
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
    /// under that bound, which the verifier checks to be below the caller's (smaller-bound rule), except inside a
    /// lambda and as the initializer of a sequence or an array, where it counts as unknown.  Any other value runs
    /// code that is not known, which has no bound (TOP): an error where the code runs under a bound.
    /// </summary>
    private void ReportApplication(IOrigin origin, Expression value, object site, string what, bool initializer) {
      var kind = analysis.Classify(value, caller);
      if (kind == ModularTerminationAnalysis.FunctionValueKind.CallbackParameter && (initializer || insideLambda.Contains(site))) {
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
      for (var i = 0; i < args.Count && i < callable.Ins.Count; i++) {
        if (!ModularTermination.HasCallbackBound(callable, i)) {
          continue;
        }
        var kind = analysis.Classify(args[i], caller);
        if (kind == ModularTerminationAnalysis.FunctionValueKind.CallbackParameter && insideLambda.Contains(site)) {
          kind = ModularTerminationAnalysis.FunctionValueKind.Unknown;
        }
        if (kind == ModularTerminationAnalysis.FunctionValueKind.Unknown) {
          reporter.Error(MessageSource.Resolver, args[i].Origin,
            $"modular termination: in {CallerName}, the value passed for parameter '{callable.Ins[i].Name}' of '{QualifiedName(callee)}', " +
            "which has a 'decreases {:callback}' bound, must be a lambda expression, a parameter with such a bound (outside a lambda), " +
            "or a function that reaches no dispatched call");
        }
      }
    }

    private bool FirstReport(IOrigin origin, bool top = false) {
      var center = origin.Center;
      return reported.Add((center.Uri, center.line, center.col, top));
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

    /// <summary>
    /// A type that the code mentions may have its constraint, its witness, or the default values of its components
    /// evaluated there.  They belong to no method or function and have no global bound, so they are code without a
    /// bound (TOP): under the same-bound rule, they cannot run under the bound of a caller that has one if they
    /// can reach open dispatch.
    /// </summary>
    private void ReportTypes(IOrigin origin, IEnumerable<Type> types, bool bounded) {
      if (analysis.ReachingType(types) is { } decl && reportedTypes.Add((origin.Center.Uri, origin.Center.line, decl))) {
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
