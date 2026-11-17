using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// Rejects a replacement that makes the module dependencies cyclic.
///
/// The module graph that the resolver checks for cycles has an edge from a replacing module R to the
/// module F it replaces, and an edge from each client of F to F.  In the compiled program every use of
/// F is a use of R, so the graph that matters has an edge from each client of F to R.  If R depends on
/// a client of F, that graph is cyclic: a member of R can call a member of the client, which calls a
/// member of F, which is the member of R.  No termination check sees such a call cycle, because each
/// call goes to an imported module, which is taken to be lower in the module order; and a type of R
/// can contain a type of the client that contains the type of F, so the checks that a type has a
/// value, and how large it is, are made on a cycle that does not exist.  A lemma in such a cycle
/// proves false (https://github.com/erniecohen/dafny/issues/405).
///
/// The rule: identify each replaceable module with the modules that replace it.  The dependencies of
/// the modules must remain acyclic, where a module depends on the modules it imports, on the modules
/// nested in it and on the module it refines.  The dependency of R on F by "replaces" is the
/// identification itself, and is not counted.  So R may not import F, and may not depend on any module
/// that depends on F, directly or through other replacements.
///
/// The check reads the resolved program, where each import names the module definition at the end of
/// its path.  The resolver's own dependency graph names the first component of the path, so in it a
/// module that imports A.B depends on everything in A; that graph is too coarse for this rule.
/// </summary>
public static class ReplacementDependencyChecker {

  public static void Check(Program program, ErrorReporter reporter) {
    var replacedBy = new Dictionary<ModuleDefinition, List<ModuleDefinition>>();
    var replacements = new List<ModuleDefinition>();
    foreach (var module in program.RawModules()) {
      if (ReplacedModule(module) is { } replaced) {
        replacements.Add(module);
        if (!replacedBy.TryGetValue(replaced, out var replacing)) {
          replacedBy[replaced] = replacing = [];
        }
        replacing.Add(module);
      }
    }

    // The order of RawModules is the order in which modules finished resolving; report in source order.
    foreach (var replacement in replacements.OrderBy(m => m.Origin.Uri?.ToString() ?? "", System.StringComparer.Ordinal)
               .ThenBy(m => m.Origin.pos)) {
      CheckReplacement(replacement, replacedBy, reporter);
    }
  }

  private static ModuleDefinition ReplacedModule(ModuleDefinition module) {
    return module.Implements is { Kind: ImplementationKind.Replacement } implements ? implements.Target.Def : null;
  }

  /// <summary>
  /// The modules that "module" uses: those it imports, those nested in it, and the module it refines.
  /// The module it replaces is not among them.
  /// </summary>
  private static IEnumerable<ModuleDefinition> Uses(ModuleDefinition module) {
    foreach (var declaration in module.TopLevelDecls) {
      switch (declaration) {
        case LiteralModuleDecl literal:
          yield return literal.ModuleDef;
          break;
        case AliasModuleDecl alias:
          yield return alias.TargetQId.Def;
          yield return alias.Signature?.ModuleDef;
          break;
        case AbstractModuleDecl abstractImport:
          yield return abstractImport.QId.Def;
          yield return abstractImport.OriginalSignature?.ModuleDef;
          yield return abstractImport.Signature?.ModuleDef;
          break;
      }
    }
    if (module.Implements is { Kind: ImplementationKind.Refinement } refinement) {
      yield return refinement.Target.Def;
    }
  }

  /// <summary>
  /// A step of the search: "Module" was reached from "Previous", either because Previous uses or replaces
  /// Module (Redirected is null), or because Previous uses Redirected, which Module replaces.
  /// </summary>
  private record Step(ModuleDefinition Module, Step Previous, ModuleDefinition Redirected);

  private static void CheckReplacement(ModuleDefinition replacement,
    Dictionary<ModuleDefinition, List<ModuleDefinition>> replacedBy, ErrorReporter reporter) {
    // The modules that "replacement" stands for: the module it replaces, the module that one replaces, and so on.
    var stoodFor = new HashSet<ModuleDefinition>();
    for (var below = ReplacedModule(replacement); below != null && stoodFor.Add(below); below = ReplacedModule(below)) {
    }

    // Breadth first, so that the path reported is a shortest one.
    var visited = new HashSet<ModuleDefinition> { replacement };
    var queue = new Queue<Step>();
    queue.Enqueue(new Step(replacement, null, null));

    void Visit(ModuleDefinition module, Step previous, ModuleDefinition redirected) {
      if (module != null && visited.Add(module)) {
        queue.Enqueue(new Step(module, previous, redirected));
      }
    }

    while (queue.Count > 0) {
      var step = queue.Dequeue();
      foreach (var used in Uses(step.Module)) {
        if (used == null) {
          continue;
        }
        if (stoodFor.Contains(used)) {
          var path = string.Join(" -> ", Path(step).Append(DisplayName(used)));
          reporter.Error(MessageSource.Resolver, replacement.Implements.Target.RootToken(),
            $"replacing module {DisplayName(used)} by {DisplayName(replacement)} makes the module dependencies cyclic, " +
            $"because {DisplayName(replacement)} uses {DisplayName(used)} other than by replacing it: {path}");
          return;
        }
        Visit(used, step, null);
        // A use of a replaced module is a use of each module that replaces it.
        var above = new List<ModuleDefinition> { used };
        for (var i = 0; i < above.Count; i++) {
          foreach (var replacing in replacedBy.GetValueOrDefault(above[i], [])) {
            if (!above.Contains(replacing)) {
              above.Add(replacing);
              Visit(replacing, step, above[i]);
            }
          }
        }
      }
      // The replacing module has what the replaced module has, its imports and nested modules included.
      Visit(ReplacedModule(step.Module), step, null);
    }
  }

  private static IEnumerable<string> Path(Step step) {
    var names = new List<string>();
    for (; step != null; step = step.Previous) {
      names.Add(step.Redirected == null
        ? DisplayName(step.Module)
        : $"{DisplayName(step.Redirected)} (replaced by {DisplayName(step.Module)})");
    }
    names.Reverse();
    return names;
  }

  private static string DisplayName(ModuleDefinition module) {
    var name = module.FullDafnyName;
    return name.Length == 0 ? module.Name : name;
  }
}
