#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// What an edge of the module dependency graph stands for.  The order matters: where two edges join the
/// same two declarations, the graph keeps the larger kind, and the kinds that may not lie on a cycle are
/// the larger ones.
/// </summary>
public enum ModuleDependencyKind {
  /// <summary>A module to an import that it declares, and an export set to its module.</summary>
  Declaration,
  /// <summary>An import ("import B", "import X = B", "import opened B", "import B`E") to the module it names.</summary>
  Import,
  /// <summary>A module to a module nested in it.</summary>
  Submodule,
  /// <summary>An abstract import ("import X : I") to the module it names.</summary>
  AbstractImport,
  /// <summary>A module to the module it refines.</summary>
  Refinement,
  /// <summary>A module to the module it replaces.</summary>
  Replacement
}

/// <summary>
/// A recursive module group: the modules of a strongly connected component of the module dependency graph
/// in which every edge is a plain import.  Members are in source order.
/// </summary>
public record RecursiveModuleGroup(IReadOnlyList<LiteralModuleDecl> Members);

/// <summary>
/// Recursion across module boundaries (--recursive-modules, issue 466).  The design is in
/// docs/dev/design/xmod-recursion.md.
///
/// Without the option, any cycle in the module dependency graph is an error, and nothing here runs.
/// With it, a strongly connected component of that graph is a recursive module group if every edge
/// inside it is a plain import.  A group means what one module with the declarations of all its members
/// would mean.  Every other kind of edge presupposes that its target is finished, so it must point to a
/// strictly lower component:
///  * a module to the module it refines or replaces: the refining module inherits proofs that were made
///    for the base alone;
///  * an abstract import to its target: it stands for an arbitrary refinement of a finished module;
///  * the binding of an inherited abstract import ("import X = M" in a module whose base declares
///    "import X : I"): the inherited calls into X are not checked again;
///  * a module to its submodules: lexical scoping in a cycle has no argument yet.
/// A replaceable module is in no group: every module that replaces it inherits its imports, so a use of
/// it from a group would be a use of its replacement that closes a cycle no check sees.
///
/// This first stage computes the groups and reports them.  A group of more than one module is still an
/// error, "not yet supported", so every program that resolves has an acyclic module graph, as before.
/// </summary>
public static class RecursiveModuleGroups {

  /// <summary>
  /// Returns false, after reporting an error, if the options are inconsistent.
  /// </summary>
  public static bool CheckOptions(Program program, ErrorReporter reporter) {
    var options = program.Options;
    if (!options.Get(CommonOptionBag.RecursiveModules)) {
      return true;
    }
    if (!options.Get(CommonOptionBag.MemberInvariants)) {
      reporter.Error(MessageSource.Resolver, Token.NoToken, "use of --recursive-modules requires --member-invariants");
      return false;
    }
    return true;
  }

  /// <summary>
  /// Classifies every strongly connected component of the module dependency graph that has a cycle.
  /// Reports an error for each edge that may not lie on a cycle, and for each group (not yet supported).
  /// Returns the groups.  Called only under --recursive-modules, in place of the report of every cycle.
  /// </summary>
  public static List<RecursiveModuleGroup> Compute(Program program, Graph<ModuleDecl> dependencies,
    Func<ModuleDecl, ModuleDecl, ModuleDependencyKind> kindOf, ErrorReporter reporter) {
    var groups = new List<RecursiveModuleGroup>();
    var components = dependencies.TopologicallySortedComponents()
      .Select(representative => dependencies.GetSCC(representative).OrderBy(Position).ToList())
      .Where(component => component.Count > 1 || HasEdge(dependencies, component[0], component[0]))
      .OrderBy(component => Position(component[0]))
      .ToList();

    foreach (var component in components) {
      var members = new HashSet<ModuleDecl>(component);
      IEnumerable<ModuleDecl> Successors(ModuleDecl from) {
        return dependencies.FindVertex(from).Successors.Select(v => v.N).Where(members.Contains).OrderBy(Position);
      }

      var errorCount = reporter.ErrorCount;
      foreach (var from in component) {
        foreach (var to in Successors(from)) {
          if (ForbiddenEdge(from, to, kindOf(from, to)) is var (origin, what)) {
            reporter.Error(MessageSource.Resolver, origin,
              $"module definition contains a cycle that goes through {what}; " +
              $"only plain imports may form a recursive module group: {CycleThrough(from, to, Successors)}");
          }
        }
      }
      foreach (var replaceable in component.OfType<LiteralModuleDecl>()
                 .Where(literal => literal.ModuleDef.ModuleKind == ModuleKindEnum.Replaceable)) {
        var next = Successors(replaceable).First();
        reporter.Error(MessageSource.Resolver, replaceable.Origin,
          $"module definition contains a cycle that goes through the replaceable module {DisplayName(replaceable)}; " +
          $"a replaceable module may not be in a recursive module group: {CycleThrough(replaceable, next, Successors)}");
      }
      if (reporter.ErrorCount != errorCount) {
        continue;
      }

      var modules = component.OfType<LiteralModuleDecl>().ToList();
      if (modules.Count < 2) {
        // A module that imports itself, or imports that name each other.  No second module is involved, so
        // this is not a group.
        var from = component[0];
        var to = Successors(from).First();
        var why = modules.Count == 1 ? "a module may not import itself" : "imports may not name each other";
        reporter.Error(MessageSource.Resolver, from.Origin,
          $"module definition contains a cycle ({why}): {CycleThrough(from, to, Successors)}");
        continue;
      }
      groups.Add(new RecursiveModuleGroup(modules));
    }

    if (groups.Count != 0) {
      CheckNoCodatatypes(program, groups[0], reporter);
    }
    foreach (var group in groups) {
      var names = group.Members.Select(DisplayName).ToList();
      foreach (var member in group.Members) {
        reporter.Info(MessageSource.Resolver, member.Origin, $"recursive module group: {string.Join(", ", names)}");
      }
      reporter.Error(MessageSource.Resolver, group.Members[0].Origin,
        $"modules {English(names)} import each other; a recursive module group of more than one module is not yet supported");
    }
    return groups;
  }

  /// <summary>
  /// If the edge may not lie on a cycle, where to report it and what to call it.
  /// </summary>
  private static (IOrigin Origin, string What)? ForbiddenEdge(ModuleDecl from, ModuleDecl to, ModuleDependencyKind kind) {
    switch (kind) {
      case ModuleDependencyKind.Refinement:
      case ModuleDependencyKind.Replacement: {
          var implements = ((LiteralModuleDecl)from).ModuleDef.Implements;
          var verb = kind == ModuleDependencyKind.Refinement ? "refinement" : "replacement";
          return (implements.Target.RootToken(), $"the {verb} of module {implements.Target} by module {DisplayName(from)}");
        }
      case ModuleDependencyKind.AbstractImport:
        return (from.Origin, $"the abstract import {from.Name}");
      case ModuleDependencyKind.Submodule:
        return (to.Origin, $"the nesting of module {to.Name} in module {DisplayName(from)}");
      case ModuleDependencyKind.Import:
        if (from is AliasModuleDecl { EnclosingModuleDefinition: { } enclosing } && InheritedAbstractImport(enclosing, from.Name) is { } owner) {
          return (from.Origin, $"import {from.Name} of module {DisplayName(enclosing)}, which binds the abstract import {from.Name} " +
                               $"inherited{(owner.Value == null ? "" : " from module " + DisplayName(owner.Value))}");
        }
        return null;
      default:
        return null;
    }
  }

  /// <summary>
  /// Whether a module inherits an abstract import of the given name through refinement or replacement.
  /// The answer is non-null if it does; its value is the module that declares the abstract import, or null
  /// if a base could not be found, in which case the import is taken to be a binding.
  ///
  /// The modules are not resolved yet, so the base of a module is found by following names through the
  /// declarations, without regard to export sets.  That finds every module the resolver would find.
  /// </summary>
  private static StrongBox<ModuleDefinition?>? InheritedAbstractImport(ModuleDefinition module, string name) {
    var seen = new HashSet<ModuleDefinition> { module };
    var current = module;
    while (current.Implements != null) {
      var baseModule = Target(current.Implements.Target, new HashSet<ModuleDecl>());
      if (baseModule == null) {
        return new StrongBox<ModuleDefinition?>(null);
      }
      if (!seen.Add(baseModule)) {
        return null;
      }
      foreach (var declaration in baseModule.TopLevelDecls.OfType<ModuleDecl>().Where(d => d.Name == name)) {
        switch (declaration) {
          case AbstractModuleDecl:
            return new StrongBox<ModuleDefinition?>(baseModule);
          case AliasModuleDecl:
            // Already bound in the base, unless the base itself bound an abstract import; either way the
            // base's binding was checked where the base is declared.
            return null;
        }
      }
      current = baseModule;
    }
    return null;
  }

  private sealed class StrongBox<T>(T value) {
    public T Value { get; } = value;
  }

  /// <summary>
  /// The module definition that a qualified module id names, found through the declarations.  Null if it
  /// is not found.
  /// </summary>
  private static ModuleDefinition? Target(ModuleQualifiedId id, HashSet<ModuleDecl> visiting) {
    var declaration = id.Root;
    for (var i = id.RootIndex + 1; declaration != null && i < id.Path.Count; i++) {
      var definition = Definition(declaration, visiting);
      var next = id.Path[i].Value;
      declaration = definition?.TopLevelDecls.OfType<ModuleDecl>().FirstOrDefault(d => d is not ModuleExportDecl && d.Name == next);
    }
    return declaration == null ? null : Definition(declaration, visiting);
  }

  private static ModuleDefinition? Definition(ModuleDecl declaration, HashSet<ModuleDecl> visiting) {
    if (!visiting.Add(declaration)) {
      return null;
    }
    var definition = declaration switch {
      LiteralModuleDecl literal => literal.ModuleDef,
      AliasModuleDecl alias => Target(alias.TargetQId, visiting),
      AbstractModuleDecl abstractImport => Target(abstractImport.QId, visiting),
      ModuleExportDecl export => export.EnclosingModuleDefinition,
      _ => null
    };
    visiting.Remove(declaration);
    return definition;
  }

  /// <summary>
  /// The rule of issue 395, for a program that has a recursive module group: it may not declare a
  /// codatatype.  The option requires --member-invariants, whose rule already covers a program that
  /// declares an invariant.
  /// </summary>
  private static void CheckNoCodatatypes(Program program, RecursiveModuleGroup group, ErrorReporter reporter) {
    var codatatypes = AllTopLevelDecls(program.DefaultModuleDef).OfType<CoDatatypeDecl>().ToList();
    if (codatatypes.Count == 0) {
      return;
    }
    var compilation = program.Compilation;
    var codatatype = codatatypes.FirstOrDefault(c => c.ShouldVerify(compilation)) ?? codatatypes[0];
    var where = codatatype.EnclosingModuleDefinition.IsDefaultModule ? "" : $" in module '{codatatype.EnclosingModuleDefinition.Name}'";
    reporter.Error(MessageSource.Resolver, group.Members[0].Origin,
      $"a program that has a recursive module group may not declare a codatatype, but it declares codatatype '{codatatype.Name}'{where}");
  }

  private static IEnumerable<TopLevelDecl> AllTopLevelDecls(ModuleDefinition module) {
    foreach (var declaration in module.TopLevelDecls) {
      yield return declaration;
      if (declaration is LiteralModuleDecl literal) {
        foreach (var inner in AllTopLevelDecls(literal.ModuleDef)) {
          yield return inner;
        }
      }
    }
  }

  private static bool HasEdge(Graph<ModuleDecl> dependencies, ModuleDecl from, ModuleDecl to) {
    return dependencies.FindVertex(from).Successors.Any(v => ReferenceEquals(v.N, to));
  }

  /// <summary>
  /// A shortest cycle that starts with the edge from "from" to "to", in the form of the cycle errors
  /// reported without the option.
  /// </summary>
  private static string CycleThrough(ModuleDecl from, ModuleDecl to, Func<ModuleDecl, IEnumerable<ModuleDecl>> successors) {
    var previous = new Dictionary<ModuleDecl, ModuleDecl?> { [to] = null };
    var queue = new Queue<ModuleDecl>();
    queue.Enqueue(to);
    while (queue.Count > 0 && !previous.ContainsKey(from)) {
      var current = queue.Dequeue();
      foreach (var next in successors(current)) {
        if (previous.TryAdd(next, current)) {
          queue.Enqueue(next);
        }
      }
    }
    var back = new List<ModuleDecl>();
    for (ModuleDecl? step = from; step != null && !ReferenceEquals(step, to); step = previous.GetValueOrDefault(step)) {
      back.Add(step);
    }
    // "back" is from, then the vertices of the path from "to" to "from" in reverse order, without "to"
    back.Reverse();
    var cycle = new List<ModuleDecl> { from };
    if (!ReferenceEquals(from, to)) {
      cycle.Add(to);
      cycle.AddRange(back.Take(back.Count - 1));
    }
    cycle.Add(from);
    return string.Join(" -> ", cycle.Select(Describe));
  }

  private static string Describe(ModuleDecl declaration) {
    return (declaration is LiteralModuleDecl ? "module " : "import ") + declaration.Name;
  }

  private static string DisplayName(ModuleDecl declaration) {
    return declaration is LiteralModuleDecl literal ? DisplayName(literal.ModuleDef) : declaration.Name;
  }

  private static string DisplayName(ModuleDefinition module) {
    var name = module.FullDafnyName;
    return name.Length == 0 ? module.Name : name;
  }

  private static string English(IReadOnlyList<string> names) {
    return names.Count == 2
      ? $"{names[0]} and {names[1]}"
      : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];
  }

  /// <summary>
  /// Source order: by file, then by position.
  /// </summary>
  private static (string, int) Position(ModuleDecl declaration) {
    return (declaration.Origin.Uri?.ToString() ?? "", declaration.Origin.pos);
  }
}
