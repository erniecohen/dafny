using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Microsoft.Dafny {
  public partial class BoogieGenerator {
    /// <summary>
    /// Whether the termination check of a call that a refining module inherits, from a member of the SCC of
    /// "vertex" to another, was made by the refinement base (https://github.com/erniecohen/dafny/issues/409).
    ///
    /// An assert at an inherited origin becomes an assume, because the base made it.  For the termination check of a
    /// call that holds only if the base checked the same call against the same decreases clauses, that is, if the
    /// caller and the callee were in one SCC of the base.  A refining module that gives a body to a member the base
    /// left without one can close a cycle through an inherited call that the base never checked, because there the
    /// callee did not call back.  So the check is assumed only when the SCC is inherited unchanged:
    ///  - every member of the SCC has a counterpart in the refinement base, all in one module;
    ///  - each has the decreases clause of its counterpart, which the refinement copies, given or inferred; and
    ///  - every call between two members of the SCC is a call between their counterparts in the base.
    /// The last condition makes the counterparts one strongly connected subgraph of the base's call graph, so the base
    /// placed the inherited call in one SCC, and checked it.  Anything else, such as a vertex that is not a member or
    /// a type, makes the SCC changed, and the check is made.
    /// </summary>
    public static bool InheritedRecursionUnchanged(ICallable vertex) {
      var graph = vertex.EnclosingModule.CallGraph;
      var representative = graph.GetSCCRepresentative(vertex);
      return InheritedRecursionUnchangedCache.GetValue(representative, r => new StrongBox<bool>(ComputeInheritedRecursionUnchanged(r))).Value;
    }

    private static readonly ConditionalWeakTable<ICallable, StrongBox<bool>> InheritedRecursionUnchangedCache = new();

    private static bool ComputeInheritedRecursionUnchanged(ICallable representative) {
      var module = representative.EnclosingModule;
      var baseModule = module.Implements?.Target.Def;
      if (baseModule == null) {
        return false;
      }
      var graph = module.CallGraph;
      var members = graph.GetSCC(representative);
      var counterparts = new Dictionary<ICallable, ICallable>();
      foreach (var member in members) {
        var counterpart = RefinementBaseCallable(member, module, baseModule);
        if (counterpart == null || counterpart.EnclosingModule != baseModule || !SameDecreases(member, counterpart, module)) {
          return false;
        }
        counterparts[member] = counterpart;
      }
      foreach (var member in members) {
        var baseVertex = baseModule.CallGraph.FindVertex(counterparts[member]);
        var baseSuccessors = baseVertex == null ? [] : baseVertex.Successors.Select(s => s.N).ToHashSet();
        foreach (var successor in graph.FindVertex(member).Successors) {
          if (counterparts.TryGetValue(successor.N, out var baseSuccessor) && !baseSuccessors.Contains(baseSuccessor)) {
            return false;
          }
        }
      }
      return true;
    }

    /// <summary>
    /// The vertex of the refinement base's call graph that a refining module copied, merged or refined as "vertex",
    /// or null.  A type is a counterpart only if the refining module copied it unchanged.
    /// </summary>
    private static ICallable RefinementBaseCallable(ICallable vertex, ModuleDefinition module, ModuleDefinition baseModule) {
      return vertex switch {
        MemberDecl member => RefinementBaseCounterpart(member) as ICallable,
        TopLevelDecl decl when decl.Origin.IsInherited(module) =>
          baseModule.TopLevelDecls.FirstOrDefault(baseDecl => baseDecl.Name == decl.Name) as ICallable,
        _ => null
      };
    }

    private static bool SameDecreases(ICallable vertex, ICallable counterpart, ModuleDefinition module) {
      var decreases = vertex.Decreases.Expressions;
      var baseDecreases = counterpart.Decreases.Expressions;
      return decreases.Count == baseDecreases.Count && decreases.All(e => e.Origin.IsInherited(module));
    }

    /// <summary>
    /// Whether a refining module must check termination in an inherited body: either its local SCC has changed,
    /// or global/callback bounds require checking calls against the refined contracts and dispatch effects.
    /// The pipeline schedules the copy; unrelated assertions in its body remain assumed. The legacy switches
    /// independently restore the old local-SCC and modular-termination rules.
    /// </summary>
    public static bool MustCheckInheritedTermination(Declaration declaration, DafnyOptions options) {
      var module = declaration switch {
        MemberDecl member => member.EnclosingClass?.EnclosingModuleDefinition,
        IteratorDecl iterator => iterator.EnclosingModuleDefinition,
        _ => null
      };
      if (module == null || !declaration.Origin.IsInherited(module)) {
        return false;
      }
      // Global and callback bounds are checked against the refined program, not just its local SCC.
      // Refinement can introduce a bound through a new parent trait, or change the contracts and
      // dispatch effects of callees while leaving this body and its own bound unchanged (#493).
      // Schedule the inherited body even when no ordinary recursive edge needs to be rechecked.
      if (ModularTermination.RulesEnabled(options) &&
          (TerminationVertices(declaration).Any(vertex =>
            ModularTermination.HasGlobalBound(vertex) || ModularTermination.HasCallbackBounds(vertex)) ||
           UsesBoundedCallable(declaration))) {
        return true;
      }
      if (options.Get(CommonOptionBag.LegacyInheritedTerminationChecks)) {
        return false;
      }
      return TerminationVertices(declaration).Any(vertex => IsRecursive(vertex) && !InheritedRecursionUnchanged(vertex));
    }

    // Even a caller at TOP must check a bounded argument against a callee's callback contract.
    // That contract can be acquired through refinement while this caller's signature stays unchanged.
    private static bool UsesBoundedCallable(Declaration declaration) {
      var pending = new Stack<INode>([declaration]);
      var seen = new HashSet<INode>();
      while (pending.TryPop(out var node)) {
        if (!seen.Add(node)) {
          continue;
        }
        if (node is Type type && BoundedCallables.Payload(type) != null ||
            node is Expression { Type: { } expressionType } && BoundedCallables.Payload(expressionType) != null) {
          return true;
        }
        foreach (var child in node.Children) {
          pending.Push(child);
        }
      }
      return false;
    }

    /// <summary>
    /// The call-graph vertices whose calls the bodies of "declaration" make: a prefix lemma's calls are recorded for
    /// its extreme lemma, an iterator's for its MoveNext method, and a function's by-method body is a vertex of its
    /// own.
    /// </summary>
    private static IEnumerable<ICallable> TerminationVertices(Declaration declaration) {
      return declaration switch {
        PrefixLemma prefixLemma => [prefixLemma.ExtremeLemma],
        IteratorDecl iterator => [iterator.Member_MoveNext],
        Function { ByMethodDecl: { } byMethod } function => [function, byMethod],
        MemberDecl and ICallable callable => [callable],
        _ => []
      };
    }

    private static bool IsRecursive(ICallable vertex) {
      var graph = vertex.EnclosingModule.CallGraph;
      var v = graph.FindVertex(vertex);
      if (v == null) {
        return false;
      }
      var representative = graph.GetSCCRepresentative(vertex);
      return v.Successors.Any(s => graph.GetSCCRepresentative(s.N) == representative);
    }

    /// <summary>
    /// The origin at which to check the termination of a call from "tok" to "callee", a member of the caller's SCC.
    /// An inherited call whose SCC is not inherited unchanged is checked, not assumed.
    /// </summary>
    private IOrigin InheritedTerminationOrigin(IOrigin tok, ICallable callee) {
      return tok.IsInherited(currentModule) && !options.Get(CommonOptionBag.LegacyInheritedTerminationChecks) &&
             !InheritedRecursionUnchanged(callee)
        ? new ForceCheckOrigin(tok)
        : tok;
    }
  }
}
