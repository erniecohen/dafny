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
public sealed class RecursiveModuleGroup {
  /// <summary>The modules of the group, in source order.</summary>
  public IReadOnlyList<LiteralModuleDecl> Members { get; }

  /// <summary>
  /// Every declaration of the component: the modules, the imports among them, and the export sets those
  /// imports name.  In source order.
  /// </summary>
  public IReadOnlyList<ModuleDecl> Declarations { get; }

  /// <summary>The call graph that the members share, so that its components are those of the group.</summary>
  public readonly Graph<ICallable> CallGraph = new();

  /// <summary>The dependencies among the inductive datatypes of all members.</summary>
  public readonly Graph<IndDatatypeDecl> DatatypeDependencies = new();

  /// <summary>The dependencies among the type synonyms, subset types and newtypes of all members.</summary>
  public readonly Graph<RedirectingTypeDecl> TypeRedirectionDependencies = new();

  /// <summary>The parent relation among the types of all members.</summary>
  public readonly Graph<TopLevelDeclWithMembers> ParentRelation = new();

  /// <summary>
  /// Which traits of the members have been determined to be reference types, or are being determined
  /// (see ModuleDefinition.DetermineReferenceTypes).  A parent trait may be in another member.
  /// </summary>
  public readonly Dictionary<TraitDecl, bool> ReferenceTraitsProgress = new();

  /// <summary>The callables for which a cycle error has been reported, so that each cycle is reported once.</summary>
  public readonly HashSet<ICallable> CycleErrorHasBeenReported = new();

  /// <summary>The resolver of each member, while the group is being resolved.</summary>
  public readonly Dictionary<ModuleDefinition, ModuleResolver> Resolvers = new();

  /// <summary>The declarations of each member, as its resolver passes them from phase to phase.</summary>
  public readonly Dictionary<ModuleDefinition, List<TopLevelDecl>> MemberDeclarations = new();

  /// <summary>State that a phase shares among the members, by the name of the phase.</summary>
  public readonly Dictionary<string, object> Shared = new();

  /// <summary>Set by a member whose round of a fixpoint phase changed something.</summary>
  public bool RoundChanged;

  /// <summary>Whether the members repeat the fixpoint phase they are in.  Set by the driver between rounds.</summary>
  public bool RepeatRound;

  private readonly HashSet<string> done = new();

  /// <summary>True the first time it is called with a given name: for the work that is done once for the group.</summary>
  public bool Once(string what) => done.Add(what);

  /// <summary>The declarations of all members, in the order of the members.</summary>
  public List<TopLevelDecl> AllDeclarations() {
    return Members.Where(m => MemberDeclarations.ContainsKey(m.ModuleDef)).SelectMany(m => MemberDeclarations[m.ModuleDef]).ToList();
  }

  public RecursiveModuleGroup(IReadOnlyList<LiteralModuleDecl> members, IReadOnlyList<ModuleDecl> declarations) {
    Members = members;
    Declarations = declarations;
  }

  public bool Contains(ModuleDefinition module) => module.RecursiveGroup == this;
}

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
/// The members of a group are resolved together, phase by phase (RecursiveModuleGroupResolver), so that
/// every analysis that works on one module at a time sees what it would see in the one module.
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
  /// Reports an error for each edge that may not lie on a cycle, and for what a member of a group may not
  /// have yet.  Returns the groups.  Called only under --recursive-modules, in place of the report of
  /// every cycle.
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
      groups.Add(new RecursiveModuleGroup(modules, component));
    }

    if (groups.Count != 0) {
      CheckNoCodatatypes(program, groups[0], reporter);
    }
    foreach (var group in groups) {
      var names = group.Members.Select(DisplayName).ToList();
      foreach (var member in group.Members) {
        reporter.Info(MessageSource.Resolver, member.Origin, $"recursive module group: {string.Join(", ", names)}");
        member.ModuleDef.RecursiveGroup = group;
        member.ModuleDef.CallGraph = group.CallGraph;
        CheckSupported(member, reporter);
        _ = member.ShouldVerify(program.Compilation); // fills in UrisToVerify
        if (program.Compilation.UrisToVerify?.Contains(member.Origin.Uri) == false) {
          // A group that is not verified here: a library does not record groups yet.
          reporter.Error(MessageSource.Resolver, member.Origin,
            $"module {DisplayName(member)}, which is in a recursive module group, is in a library; this is not yet supported");
        }
      }
    }
    if (groups.Count != 0) {
      // A module outside a group that refines or replaces a member, and an abstract import of a member.
      // The design allows them (the member is in a strictly lower component), but the refinement of a
      // group is a later stage.
      var groupOf = groups.SelectMany(g => g.Members.Select(m => (ModuleDecl)m)).ToHashSet();
      foreach (var from in dependencies.GetVertices().Select(v => v.N).OrderBy(Position)) {
        foreach (var to in dependencies.FindVertex(from).Successors.Select(v => v.N).Where(groupOf.Contains).OrderBy(Position)) {
          var kind = kindOf(from, to);
          if (kind is ModuleDependencyKind.Refinement or ModuleDependencyKind.Replacement) {
            reporter.Error(MessageSource.Resolver, ((LiteralModuleDecl)from).ModuleDef.Implements.Target.RootToken(),
              $"module {DisplayName(from)} refines or replaces module {DisplayName(to)}, which is in a recursive module group; this is not yet supported");
          } else if (kind == ModuleDependencyKind.AbstractImport && from.EnclosingModuleDefinition?.RecursiveGroup == null) {
            reporter.Error(MessageSource.Resolver, from.Origin,
              $"the abstract import {from.Name} names module {DisplayName(to)}, which is in a recursive module group; this is not yet supported");
          }
        }
      }
    }
    return groups;
  }

  /// <summary>
  /// What a member of a group may not have yet.  Each of these is sound to add only with a rule of its own
  /// (sections 3 and 4 of the design), so until then it is an error.
  /// </summary>
  private static void CheckSupported(LiteralModuleDecl member, ErrorReporter reporter) {
    var module = member.ModuleDef;
    var name = DisplayName(member);
    const string notYet = "this is not yet supported";
    if (module.ModuleKind == ModuleKindEnum.Abstract) {
      reporter.Error(MessageSource.Resolver, member.Origin,
        $"abstract module {name} is in a recursive module group; {notYet}");
    }
    if (module.Implements != null) {
      reporter.Error(MessageSource.Resolver, module.Implements.Target.RootToken(),
        $"module {name}, which is in a recursive module group, refines or replaces module {module.Implements.Target}; {notYet}");
    }
    foreach (var declaration in module.TopLevelDecls) {
      switch (declaration) {
        case AbstractModuleDecl:
          reporter.Error(MessageSource.Resolver, declaration.Origin,
            $"module {name}, which is in a recursive module group, declares the abstract import {declaration.Name}; {notYet}");
          break;
        case TopLevelDeclWithMembers type:
          foreach (var invariant in type.Members.OfType<MemberInvariant>()) {
            reporter.Error(MessageSource.Resolver, invariant.NameNode.Origin,
              $"{type.WhatKind} '{type.Name}' declares an invariant in module {name}, which is in a recursive module group; {notYet}");
          }
          break;
      }
    }
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

/// <summary>
/// Resolves the members of a recursive module group together.
///
/// A group means what one module with the declarations of all its members would mean.  The resolver
/// works on one module in a fixed sequence of phases, and a phase may rely on every earlier phase being
/// done for every declaration of the module.  So each member has a resolver of its own, with its own
/// names and visibility scopes, and each phase is done for every member before the next phase starts
/// (LiteralModuleDecl.ResolveInPhases).  Where a phase of the one module works on all declarations at
/// once, the members share the state of that phase through the group: the call graph, the dependency
/// graphs of types, the type inference of signatures, and the work that is done once or to a fixpoint.
///
/// If a phase leaves an error in any member, no member goes on, and no member counts as resolved: a
/// later phase of one member may rely on what the phase established for another.
/// </summary>
public static class RecursiveModuleGroupResolver {
  /// <summary>
  /// Returns a result for every declaration of the group: its modules, the imports among them, and the
  /// export sets those imports name.
  /// </summary>
  public static Dictionary<ModuleDecl, ModuleResolutionResult> Resolve(ProgramResolver programResolver,
    RecursiveModuleGroup group, CompilationData compilation) {
    var members = group.Members;
    var count = members.Count;
    var resolvers = new ModuleResolver[count];
    var phases = new IEnumerator<string>[count];
    var scopes = new Type.ScopeState[count];
    var callerScopes = Type.SaveScopes();
    for (var i = 0; i < count; i++) {
      resolvers[i] = new ModuleResolver(programResolver, members[i].Options) { Group = group };
      group.Resolvers[members[i].ModuleDef] = resolvers[i];
      scopes[i] = new Type.ScopeState(new List<VisibilityScope>(), true);
      phases[i] = members[i].ResolveInPhases(resolvers[i], compilation).GetEnumerator();
    }

    bool HasErrors() => resolvers.Any(resolver => resolver.Reporter.HasErrors);

    var completed = false;
    try {
      while (true) {
        string? phase = null;
        var finished = 0;
        var inStep = true;
        for (var i = 0; i < count; i++) {
          // Each member has its own stack of visibility scopes, and a phase may leave scopes pushed.
          Type.RestoreScopes(scopes[i]);
          if (phases[i].MoveNext()) {
            phase ??= phases[i].Current;
            inStep = inStep && phase == phases[i].Current;
          } else {
            finished++;
          }
          scopes[i] = Type.SaveScopes();
        }
        if (finished == count) {
          completed = true;
          break;
        }
        if (finished != 0 || !inStep || HasErrors()) {
          break;
        }
        AfterPhase(phase!, group, resolvers);
        if (HasErrors()) {
          break;
        }
      }
    }
    finally {
      Type.RestoreScopes(callerScopes);
    }

    if (!completed || HasErrors()) {
      if (!HasErrors()) {
        // A member stopped without an error of its own: a module that it imports from outside the group
        // was not resolved.
        resolvers[0].Reporter.Error(MessageSource.Resolver, members[0].Origin,
          "not resolving the modules of the recursive module group of module '{0}' because there were errors in a module that one of them imports",
          members[0].Name);
      }
      foreach (var member in members) {
        member.ModuleDef.SuccessfullyResolved = false;
      }
    }

    var results = new Dictionary<ModuleDecl, ModuleResolutionResult>();
    for (var i = 0; i < count; i++) {
      var signatures = new Dictionary<ModuleDefinition, ModuleSignature>();
      if (members[i].Signature != null) {
        signatures[members[i].ModuleDef] = members[i].Signature;
      }
      results[members[i]] = new ModuleResolutionResult(members[i], (BatchErrorReporter)resolvers[i].Reporter, signatures,
        resolvers[i].moduleClassMembers);
    }
    foreach (var declaration in group.Declarations) {
      if (!results.ContainsKey(declaration)) {
        if (declaration is AliasModuleDecl { Signature: null } alias) {
          alias.Signature = new ModuleSignature(); // the group was given up before its imports were resolved
        }
        results[declaration] = new ModuleResolutionResult(declaration, new BatchErrorReporter(declaration.Options), new(), new());
      }
    }
    return results;
  }

  /// <summary>
  /// What is done for the group after every member has finished a phase.
  /// </summary>
  /// <summary>
  /// Whether every import on the path of the target of "import" has a signature, so that the target can
  /// be looked up.  A name that is not found counts as resolved: looking the target up reports it.
  /// </summary>
  private static bool PathIsResolved(AliasModuleDecl import) {
    var target = import.TargetQId;
    var decl = target.Root;
    for (var k = target.RootIndex + 1; decl != null; k++) {
      if (decl is AliasModuleDecl { Signature: null }) {
        return false;
      }
      if (k >= target.Path.Count) {
        break;
      }
      var signature = decl is LiteralModuleDecl literal ? literal.DefaultExport : decl.Signature;
      decl = signature?.TopLevels.GetValueOrDefault(target.Path[k].Value) as ModuleDecl;
    }
    return true;
  }

  private static void AfterPhase(string phase, RecursiveModuleGroup group, ModuleResolver[] resolvers) {
    if (phase.EndsWith('*')) {
      // A fixpoint phase: the members repeat it until a round changes nothing in any member.
      group.RepeatRound = group.RoundChanged;
      group.RoundChanged = false;
    }
    if (phase != "exportSignatures") {
      return;
    }

    // Every member now has the signatures of its export sets, so the imports among the members can be
    // resolved.  Outside a group, an import is resolved after the module it names.
    var imports = group.Declarations.OfType<AliasModuleDecl>().ToList();
    ModuleResolver ResolverOf(AliasModuleDecl import) {
      return group.Resolvers.GetValueOrDefault(import.EnclosingModuleDefinition) ?? resolvers[0];
    }
    // An import may name its target through another import (`import A.I` beside `import A`), so an import
    // waits until the imports on its path have signatures.
    var waiting = new List<AliasModuleDecl>(imports);
    while (waiting.Count > 0) {
      var ready = waiting.Where(PathIsResolved).ToList();
      if (ready.Count == 0) {
        foreach (var import in waiting) {
          ResolverOf(import).Reporter.Error(MessageSource.Resolver, import.Origin,
            $"import {import.Name} names its target through imports of its recursive module group that depend on each other");
          import.Signature = new ModuleSignature();
        }
        break;
      }
      foreach (var import in ready) {
        var resolver = ResolverOf(import);
        if (resolver.ResolveExport(import, import.EnclosingModuleDefinition, import.TargetQId, import.Exports, out var signature, resolver.Reporter)) {
          import.Signature = signature;
        } else {
          import.Signature = new ModuleSignature(); // there was an error, give it a valid but empty signature
        }
        waiting.Remove(import);
      }
    }
    if (resolvers.Any(resolver => resolver.Reporter.HasErrors)) {
      return;
    }

    // An export set, and a module, sees what its imports export.  Outside a group, the scopes of an
    // import are complete when its importer is resolved, so one pass is enough.  In a group, the scopes
    // grow together until nothing changes.
    bool changed;
    do {
      changed = false;
      foreach (var import in imports.Where(import => import.Exports.Count > 1)) {
        // The signature of an import of several export sets is a copy of their union.  Take the copy again,
        // since the scopes of those export sets may have grown.
        var resolver = ResolverOf(import);
        if (resolver.ResolveExport(import, import.EnclosingModuleDefinition, import.TargetQId, import.Exports, out var signature,
              new BatchErrorReporter(import.Options))) {
          import.Signature = signature;
        }
      }
      for (var i = 0; i < resolvers.Length; i++) {
        if (resolvers[i].PropagateVisibilityOfExportedImports(group.Members[i].Signature)) {
          changed = true;
        }
      }
    } while (changed);
  }
}
