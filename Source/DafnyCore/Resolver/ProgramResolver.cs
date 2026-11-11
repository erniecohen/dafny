using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny.Compilers;

namespace Microsoft.Dafny;

public class ProgramResolver {
  internal Action<ModuleDecl> ResolutionWorkerStarting { get; set; }
  public Program Program { get; }

  private readonly Dictionary<TopLevelDeclWithMembers, Dictionary<string, MemberDecl>> classMembers = new();

  protected readonly Graph<ModuleDecl> dependencies = new();
  public DafnyOptions Options => Program.Options;
  public SystemModuleManager SystemModuleManager => Program.SystemModuleManager;
  public ErrorReporter Reporter => Program.Reporter;

  public ProgramResolver(Program program) {
    Program = program;
  }

  public Dictionary<string, MemberDecl> GetClassMembers(TopLevelDeclWithMembers key) {
    return classMembers.GetValueOrDefault(key);
  }

  protected void ResetCardinalityValidationReceipt() {
    Program.CardinalityValidationReceipt = null;
  }

  public virtual Task Resolve(CancellationToken cancellationToken) {
    ResetCardinalityValidationReceipt();
    cancellationToken.ThrowIfCancellationRequested();
    Type.ResetScopes();

    Type.EnableScopes();
    // For the formatter, we ensure we take snapshots of the PrefixNamedModules and topleveldecls
    Program.DefaultModuleDef.PreResolveSnapshotForFormatter();

    // Changing modules at this stage without changing their CloneId doesn't break resolution caching,
    // because ResolvedPrefixNamedModules end up in the dependencies of a module so they change its hash anyways
    Program.DefaultModuleDef.ProcessPrefixNamedModules();

    var startingErrorCount = Reporter.ErrorCount;
    ComputeModuleDependencyGraph(Program, out var moduleDeclarationPointers);

    if (Reporter.ErrorCount != startingErrorCount) {
      return Task.CompletedTask;
    }

    var sortedDecls = dependencies.TopologicallySortedComponents();
    Program.ModuleSigs = new();

    if (Options.Get(CommonOptionBag.ResolutionThreads) > 0) {
      foreach (var declaration in sortedDecls) {
        declaration.Height = dependencies.FindVertex(declaration).Successors.Select(v => v.N.Height + 1).DefaultIfEmpty(0).Max();
        if (declaration is LiteralModuleDecl literal) { literal.ModuleDef.Height = declaration.Height; }
      }
    } else {
      SetHeights(sortedDecls);
    }

    using var systemNames = Options.Get(CommonOptionBag.ResolutionThreads) > 0 ? new ModuleNamingScope("_System") : null;
    var systemClassMembers = ResolveSystemModule(Program);
    foreach (var moduleClassMembers in systemClassMembers) {
      classMembers[moduleClassMembers.Key] = moduleClassMembers.Value;
    }

    var rewriters = RewriterCollection.GetRewriters(Reporter, Program);

    var compilation = Program.Compilation;
    foreach (var rewriter in rewriters) {
      cancellationToken.ThrowIfCancellationRequested();
      rewriter.PreResolve(Program);
    }

    if (Options.Get(CommonOptionBag.ResolutionThreads) > 0) {
      ResolveModulesInParallel(sortedDecls, moduleDeclarationPointers, cancellationToken);
    } else {
      foreach (var decl in sortedDecls) {
        cancellationToken.ThrowIfCancellationRequested();
        var moduleResolutionResult = ResolveModuleDeclaration(compilation, decl);
        ProcessDeclarationResolutionResult(moduleDeclarationPointers, decl, moduleResolutionResult);
      }
    }

    if (Reporter.ErrorCount != startingErrorCount) {
      return Task.CompletedTask;
    }

    Type.DisableScopes();

    InstantiateReplaceableModules(Program);
    CheckDuplicateModuleNames(Program);

    foreach (var rewriter in rewriters) {
      cancellationToken.ThrowIfCancellationRequested();
      rewriter.PostResolve(Program);
    }

    // Module cache hits still reach this phase. A downstream implementation can
    // introduce a cardinality cycle without changing any imported module.
    if (Reporter.ErrorCount == 0) {
      cancellationToken.ThrowIfCancellationRequested();
      CardinalityValidator.Validate(Program, cancellationToken);
    }
    return Task.CompletedTask;
  }

  /// <summary>
  /// A dependency-ready queue, with publication to dependents distinct from ordered
  /// publication to the program and its reporter. No height barrier is introduced.
  /// </summary>
  private void ResolveModulesInParallel(List<ModuleDecl> declarations,
    Dictionary<ModuleDecl, Action<ModuleDecl>> pointers, CancellationToken cancellationToken) {
    var indices = declarations.Select((declaration, index) => (declaration, index))
      .ToDictionary(p => p.declaration, p => p.index);
    var prerequisites = declarations.Select(d => dependencies.FindVertex(d).Successors
      .Select(v => indices[v.N]).Distinct().Order().ToArray()).ToArray();
    var dependents = declarations.Select(_ => new List<int>()).ToArray();
    var remaining = prerequisites.Select(p => p.Length).ToArray();
    var ready = new SortedSet<int>();
    var results = new ModuleResolutionResult[declarations.Count];
    var catalog = new ResolutionBuiltinCatalog(SystemModuleManager);
    var initialBuiltins = new SystemModuleManager(SystemModuleManager);
    var initialMembers = new Dictionary<TopLevelDeclWithMembers, Dictionary<string, MemberDecl>>(classMembers);
    var profile = PipelineProfile.For(Options);
    Program.ResolutionKeys = new();
    for (var i = 0; i < declarations.Count; i++) {
      foreach (var dependency in prerequisites[i]) {
        dependents[dependency].Add(i);
      }
      if (remaining[i] == 0) {
        ready.Add(i);
      }
      var declaration = declarations[i];
      if (declaration is LiteralModuleDecl literal) {
        _ = literal.ModuleDef.VisibilityScope;
      }
      var key = ModuleResolutionKey.Compute(declaration.FullName, ModuleSource(declaration), Options.VersionNumber,
        ModuleResolutionKey.Options(declaration.Options), prerequisites[i].Select(j =>
          new ModuleResolutionKey.Prerequisite(DependencyRole(declaration, declarations[j]),
            declarations[j].FullName, Program.ResolutionKeys[declarations[j]])));
      Program.ResolutionKeys[declaration] = key;
      if (declaration is LiteralModuleDecl module) {
        module.ModuleDef.ResolutionKey = key;
      }
    }

    var gate = new object();
    ExceptionDispatchInfo failure = null;
    var completed = 0;
    using var registration = cancellationToken.Register(() => {
      lock (gate) { Monitor.PulseAll(gate); }
    });

    void RunWorker() {
      try {
        while (true) {
          int index;
          lock (gate) {
            while (ready.Count == 0 && completed != declarations.Count && failure == null && !cancellationToken.IsCancellationRequested) {
              Monitor.Wait(gate);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (failure != null || completed == declarations.Count) { return; }
            index = ready.Min;
            ready.Remove(index);
          }

          Type.ResetScopes();
          Type.EnableScopes();
          try {
            using var names = new ModuleNamingScope(declarations[index].FullName);
            var builtins = new SystemModuleManager(initialBuiltins, catalog);
            builtins.Bitwidths.Clear();
            var workerProgram = new Program(Program.FullName, Program.DefaultModule, builtins,
              new BatchErrorReporter(Options), Program.Compilation) { ModuleSigs = new() };
            foreach (var file in Program.Files) { workerProgram.Files.Add(file); }
            var worker = new ProgramResolver(workerProgram);
            foreach (var member in initialMembers) { worker.classMembers[member.Key] = member.Value; }
            var closure = new SortedSet<int>();
            void AddPrerequisites(int current) {
              foreach (var prerequisite in prerequisites[current]) {
                if (closure.Add(prerequisite)) { AddPrerequisites(prerequisite); }
              }
            }
            AddPrerequisites(index);
            foreach (var prerequisite in closure) {
              var result = results[prerequisite];
              builtins.MergeResolutionView(result.Builtins);
              foreach (var members in result.SystemClassMembers.Concat(result.ClassMembers)) {
                worker.classMembers[members.Key] = members.Value;
              }
              foreach (var signature in result.Signatures) { workerProgram.ModuleSigs[signature.Key] = signature.Value; }
            }
            var start = profile?.Begin();
            ResolutionWorkerStarting?.Invoke(declarations[index]);
            var resolved = worker.ResolveModuleDeclaration(Program.Compilation, declarations[index]);
            var translationInputs = new ModuleTranslationInputs(resolved.ErrorReporter.HasErrors ? null : declarations[index],
              closure.Select(i => results[i].TranslationInputs));
            translationInputs.Bitwidths.UnionWith(builtins.Bitwidths);
            builtins.RestrictResolutionOutput(translationInputs);
            foreach (var definition in resolved.Signatures.Keys) {
              translationInputs.ModuleNames.Add(definition.FullName);
              definition.TranslationInputs = translationInputs;
              definition.ResolutionKey = Program.ResolutionKeys[declarations[index]];
            }
            var output = resolved with {
              Key = Program.ResolutionKeys[declarations[index]], Builtins = builtins,
              SystemClassMembers = worker.classMembers.Where(p => p.Key.EnclosingModuleDefinition.Name == "_System" &&
                  translationInputs.BuiltinNames.Contains(p.Key.Name))
                .ToDictionary(p => p.Key, p => p.Value),
              TranslationInputs = translationInputs
            };
            if (output.ResolvedDeclaration is LiteralModuleDecl resolvedModule) {
              resolvedModule.ModuleDef.TranslationInputs = output.TranslationInputs;
            }
            if (profile != null) {
              profile.Span("resolve.module", start.Value, new Dictionary<string, object> {
                ["module"] = declarations[index].FullName, ["key"] = output.Key, ["cached"] = false
              });
            }
            lock (gate) {
              results[index] = output;
              // A parent cannot be running: nesting is a prerequisite edge. Imports
              // cannot run until all their prerequisite declarations have completed.
              if (!ReferenceEquals(declarations[index], output.ResolvedDeclaration)) {
                pointers[declarations[index]](output.ResolvedDeclaration);
              }
              completed++;
              foreach (var dependent in dependents[index]) {
                if (--remaining[dependent] == 0) { ready.Add(dependent); }
              }
              Monitor.PulseAll(gate);
            }
          } finally {
            Type.ResetScopes();
          }
        }
      } catch (Exception error) {
        lock (gate) {
          failure ??= ExceptionDispatchInfo.Capture(error);
          Monitor.PulseAll(gate);
        }
      }
    }

    // Plugins have arbitrary state and no concurrent-resolution contract. Retain
    // module-local outputs and names, but execute their callbacks on one worker.
    var count = Options.Plugins.Except(DafnyOptions.DefaultPlugins).Any()
      ? 1 : Math.Min(Options.Get(CommonOptionBag.ResolutionThreads), declarations.Count);
    var workers = Enumerable.Range(0, count).Select(_ => new Thread(RunWorker, 0x10000000) { IsBackground = true }).ToArray();
    foreach (var worker in workers) { worker.Start(); }
    foreach (var worker in workers) { worker.Join(); }
    failure?.Throw();
    cancellationToken.ThrowIfCancellationRequested();

    for (var i = 0; i < declarations.Count; i++) {
      var result = results[i];
      SystemModuleManager.MergeResolutionView(result.Builtins);
      foreach (var members in result.SystemClassMembers) { classMembers[members.Key] = members.Value; }
      ProcessDeclarationResolutionResult(pointers, declarations[i], result);
    }
  }

  private static string ModuleSource(ModuleDecl declaration) {
    if (declaration is LiteralModuleDecl { ModuleDef.IsDefaultModule: true } literal) {
      return string.Join("\n", literal.ModuleDef.TopLevelDecls.Select(d => d.EntireRange.PrintOriginal()));
    }
    return declaration.EntireRange.PrintOriginal();
  }

  private static string DependencyRole(ModuleDecl declaration, ModuleDecl prerequisite) {
    if (declaration is LiteralModuleDecl literal) {
      return literal.ModuleDef.Implements?.Target.Root == prerequisite ? "refinement" : "nested";
    }
    return declaration switch {
      AliasModuleDecl => "import", AbstractModuleDecl => "abstract-import", ModuleExportDecl => "export-owner", _ => "resolution"
    };
  }

  public virtual void AddSystemClass(TopLevelDeclWithMembers topLevelDeclWithMembers, Dictionary<string, MemberDecl> memberDictionary) {
    classMembers[topLevelDeclWithMembers] = memberDictionary;
  }

  private void ProcessDeclarationResolutionResult(Dictionary<ModuleDecl, Action<ModuleDecl>> moduleDeclarationPointers, ModuleDecl decl,
    ModuleResolutionResult moduleResolutionResult) {
    moduleDeclarationPointers[decl](moduleResolutionResult.ResolvedDeclaration);

    foreach (var sig in moduleResolutionResult.Signatures) {
      Program.ModuleSigs[sig.Key] = sig.Value;
    }

    foreach (var moduleClassMembers in moduleResolutionResult.ClassMembers) {
      classMembers[moduleClassMembers.Key] = moduleClassMembers.Value;
    }

    foreach (var diagnostic in moduleResolutionResult.ErrorReporter.AllMessages) {
      Reporter.MessageCore(diagnostic);
    }
  }

  /// <summary>
  /// We determine where pointers to module declarations occur, and store those so caching can later set those.
  /// </summary>
  private void ComputeModuleDependencyGraph(Program program, out Dictionary<ModuleDecl, Action<ModuleDecl>> moduleDeclarationPointers) {
    var startingErrorCount = Reporter.ErrorCount;
    var rootBindings = new ModuleBindings(null);
    // TODO can we delete rootBindings and pass null instead?
    var defaultModuleBindings = program.DefaultModuleDef.BindModuleNames(this, rootBindings);
    rootBindings.BindName(program.DefaultModule.Name, program.DefaultModule, defaultModuleBindings);

    if (Reporter.ErrorCount != startingErrorCount) {
      // if there were errors, then the implicit ModuleBindings data structure invariant
      // is violated, so Processing dependencies will not succeed.
      moduleDeclarationPointers = null;
      return;
    }

    moduleDeclarationPointers = new();
    moduleDeclarationPointers[program.DefaultModule] = v => program.DefaultModule = (LiteralModuleDecl)v;
    ProcessDependencies(program.DefaultModule, defaultModuleBindings, moduleDeclarationPointers);

    // check for cycles in the import graph
    foreach (var cycle in dependencies.AllCycles()) {
      ModuleResolver.ReportCycleError(Reporter, cycle, m => m.Origin,
        m => (m is AliasModuleDecl ? "import " : "module ") + m.Name,
        "module definition contains a cycle (note: parent modules implicitly depend on submodules)");
    }
  }

  protected virtual Dictionary<TopLevelDeclWithMembers, Dictionary<string, MemberDecl>> ResolveSystemModule(Program program) {
    var systemModuleResolver = new ModuleResolver(this, Options);

    SystemModuleManager.systemNameInfo = SystemModuleManager.SystemModule.RegisterTopLevelDecls(systemModuleResolver, false);
    systemModuleResolver.moduleInfo = SystemModuleManager.systemNameInfo;

    systemModuleResolver.RevealAllInScope(SystemModuleManager.SystemModule.TopLevelDecls, SystemModuleManager.systemNameInfo.VisibilityScope);
    SystemModuleManager.ResolveValueTypeDecls(this);

    if (Options.Get(CommonOptionBag.TypeSystemRefresh)) {
      PreTypeResolver.ResolveDeclarations(
        SystemModuleManager.SystemModule.TopLevelDecls.Where(d => d is not ClassDecl).ToList(),
        systemModuleResolver, true);
    }

    // The SystemModule is constructed with all its members already being resolved. Except for
    // the non-null type corresponding to class types.  They are resolved here:
    var systemModuleClassesWithNonNullTypes =
      SystemModuleManager.SystemModule.TopLevelDecls.Where(d => (d as ClassLikeDecl)?.NonNullTypeDecl != null).ToList();
    foreach (var cl in systemModuleClassesWithNonNullTypes) {
      var d = ((ClassLikeDecl)cl).NonNullTypeDecl;
      systemModuleResolver.allTypeParameters.PushMarker();
      systemModuleResolver.ResolveTypeParameters(d.TypeArgs, true, d);
      systemModuleResolver.ResolveType(d.Origin, d.Rhs, d, ResolveTypeOptionEnum.AllowPrefix, d.TypeArgs);
      systemModuleResolver.allTypeParameters.PopMarker();
    }

    systemModuleResolver.ResolveTopLevelDecls_Core(
      ModuleDefinition.AllDeclarationsAndNonNullTypeDecls(systemModuleClassesWithNonNullTypes).ToList(),
      new Graph<IndDatatypeDecl>(), new Graph<CoDatatypeDecl>(), SystemModuleManager.SystemModule.Name, false);

    return systemModuleResolver.moduleClassMembers;
  }

  protected virtual ModuleResolutionResult ResolveModuleDeclaration(CompilationData compilation, ModuleDecl decl) {
    var moduleResolver = new ModuleResolver(this, decl.Options);
    return moduleResolver.ResolveModuleDeclaration(compilation, decl);
  }

  private static void SetHeights(IEnumerable<ModuleDecl> sortedDecls) {
    foreach (var withIndex in sortedDecls.Zip(Enumerable.Range(0, int.MaxValue))) {
      var md = withIndex.First;
      md.Height = withIndex.Second;
      if (md is LiteralModuleDecl literalModuleDecl) {
        var mdef = literalModuleDecl.ModuleDef;
        mdef.Height = withIndex.Second;
      }
    }
  }

  /// <summary>
  /// Check that no two modules that are being compiled have the same CompileName.
  ///
  /// This could happen if they are given the same name using the 'extern' declaration modifier.
  /// </summary>
  /// <param name="program">The Dafny program being compiled.</param>
  private void CheckDuplicateModuleNames(Program program) {
    // Check that none of the modules have the same CompileName.
    Dictionary<string, ModuleDefinition> compileNameMap = new Dictionary<string, ModuleDefinition>();
    foreach (ModuleDefinition m in program.CompileModules) {
      if (!m.CanCompile() || !ShouldCompile(m)) {
        // the purpose of an abstract module is to skip compilation
        continue;
      }

      string compileName = m.GetCompileName(Options);
      if (compileNameMap.TryGetValue(compileName, out var priorModDef)) {
        Reporter.Error(MessageSource.Resolver, m.Origin,
          "modules '{0}' and '{1}' both have CompileName '{2}'",
          priorModDef.Origin.val, m.Origin.val, compileName);
      } else {
        compileNameMap.Add(compileName, m);
      }
    }
  }

  public static bool ShouldCompile(IAttributeBearingDeclaration m) {
    var compileIt = true;
    Attributes.ContainsBool(m.Attributes, "compile", ref compileIt);
    return compileIt;
  }

  protected void InstantiateReplaceableModules(Program dafnyProgram) {
    foreach (var compiledModule in dafnyProgram.Modules().OrderByDescending(m => m.Height)) {
      if (compiledModule.Implements is { Kind: ImplementationKind.Replacement }) {
        var target = compiledModule.Implements.Target.Def;
        var replacement = Program.Replacements.GetValueOrDefault(target);
        if (replacement != null) {
          Reporter!.Error(MessageSource.Resolver, new NestedOrigin(compiledModule.Origin, replacement.Origin,
              $"other replacing module"),
            "a replaceable module may only be replaced once");
        } else {
          Program.Replacements[target] = Program.Replacements.GetValueOrDefault(compiledModule, compiledModule);
        }
      }
    }
  }

  public static string ModuleNotFoundErrorMessage(int i, List<Name> path, string tail = "") {
    Contract.Requires(path != null);
    Contract.Requires(0 <= i && i < path.Count);
    var addendum = 1 < path.Count ? $" (position {i} in path {Util.Comma(".", path, x => x.Value)}){tail}" : "";
    return
      $"module {path[i].Value} does not exist" + addendum;
  }

  private void ProcessDependenciesDefinition(LiteralModuleDecl literalDecl, ModuleBindings bindings,
    IDictionary<ModuleDecl, Action<ModuleDecl>> declarationPointers) {
    var module = literalDecl.ModuleDef;
    if (module.Implements != null) {
      var refinementTarget = module.Implements.Target;
      bool res = bindings.ResolveQualifiedModuleIdRootRefines(literalDecl.ModuleDef, refinementTarget, out var other);
      if (res) {
        other = SkipEnclosingModules(refinementTarget, other);
      }
      refinementTarget.Root = other;
      if (!res) {
        Reporter.Error(MessageSource.Resolver, refinementTarget.RootToken(),
          $"module {module.Implements.Target} named as {module.Implements.Kind.ToString().ToLower()} base does not exist");
      } else {
        declarationPointers.AddOrUpdate(other, v => refinementTarget.Root = v, Util.Concat);
        if (other is LiteralModuleDecl otherLiteral && otherLiteral.ModuleDef == module) {
          Reporter.Error(MessageSource.Resolver, refinementTarget.RootToken(), "module cannot refine itself: {0}",
            module.Implements.Target.ToString());
        } else {
          Contract.Assert(other != null); // follows from postcondition of TryGetValue
          dependencies.AddEdge(literalDecl, other);
        }
      }
    }

    foreach (var pointer in module.TopLevelDeclPointers) {
      if (pointer.Get() is ModuleDecl moduleDecl) {
        declarationPointers.Add(moduleDecl, v => {
          pointer.Set(v);
          v.EnclosingModuleDefinition = module;
          if (v is LiteralModuleDecl literalModuleDecl) {
            literalModuleDecl.ModuleDef.EnclosingModule = module;
          }
        });
      }
    }

    enclosingModules.Add(literalDecl);
    foreach (var toplevel in module.TopLevelDecls) {
      if (toplevel is not ModuleDecl moduleDecl) {
        continue;
      }

      if (toplevel is ModuleExportDecl) {
        dependencies.AddEdge(moduleDecl, literalDecl);
      } else {
        dependencies.AddEdge(literalDecl, moduleDecl);
      }

      var subBindings = bindings.SubBindings(moduleDecl.Name);
      ProcessDependencies(moduleDecl, subBindings ?? bindings, declarationPointers);
      if (module.ModuleKind == ModuleKindEnum.Concrete && (moduleDecl as AbstractModuleDecl)?.QId.Root != null) {
        Reporter.Error(MessageSource.Resolver, moduleDecl.Origin,
          "The abstract import named {0} (using :) may only be used in an abstract or replaceable module declaration",
          moduleDecl.Name);
      }
    }
    enclosingModules.RemoveAt(enclosingModules.Count - 1);
  }

  /// <summary>
  /// The literal modules that enclose the module declaration being processed, outermost first.
  /// </summary>
  private readonly List<LiteralModuleDecl> enclosingModules = new();

  /// <summary>
  /// The root of a qualified module id, as found by the lexical lookup, may be a module that encloses
  /// the qualified id, as A does in "module A.C refines A.B" and in "module A { module C { import A.B } }".
  /// Every enclosing module depends on the modules nested in it, so a dependency on the root would always
  /// be a cycle.  Instead, the components that name enclosing modules are skipped: the next component is
  /// looked up among the literal modules declared in the enclosing module, which is the lookup that the
  /// same name gets lexically from inside that module, and the dependency is on the module found.
  /// The skipping stops at the first component that does not name an enclosing module, at the last
  /// component, and at a component that is not a literal module of the enclosing one (an import, an
  /// export set, or a missing name).  If it stops at an enclosing module, the dependency is a genuine
  /// cycle, and the root is left as the lexical lookup found it.  Only qualified ids that previously
  /// always gave a cycle error are affected.
  /// Only with --ancestor-qualified-module-ids; without it, the root is returned unchanged.
  /// Sets qid.RootIndex and returns the new root.
  /// </summary>
  private ModuleDecl SkipEnclosingModules(ModuleQualifiedId qid, ModuleDecl root) {
    if (!Options.Get(CommonOptionBag.AncestorQualifiedModuleIds)) {
      qid.RootIndex = 0;
      return root;
    }
    var index = 0;
    var current = root;
    while (index + 1 < qid.Path.Count && IsEnclosingModule(current)) {
      var nextName = qid.Path[index + 1].Value;
      var next = ((LiteralModuleDecl)current).ModuleDef.TopLevelDecls.OfType<LiteralModuleDecl>()
        .FirstOrDefault(d => d.Name == nextName);
      if (next == null) {
        break;
      }
      current = next;
      index++;
    }
    if (IsEnclosingModule(current)) {
      // still a genuine cycle: keep the root, and so the error message, that the lexical lookup gave
      qid.RootIndex = 0;
      return root;
    }
    qid.RootIndex = index;
    return current;
  }

  private bool IsEnclosingModule(ModuleDecl decl) {
    return decl is LiteralModuleDecl literal && enclosingModules.Any(m => ReferenceEquals(m, literal));
  }

  private void ProcessDependencies(ModuleDecl moduleDecl, ModuleBindings bindings,
    IDictionary<ModuleDecl, Action<ModuleDecl>> declarationPointers) {
    dependencies.AddVertex(moduleDecl);
    if (moduleDecl is LiteralModuleDecl literalDecl) {
      ProcessDependenciesDefinition(literalDecl, bindings, declarationPointers);
    } else if (moduleDecl is AliasModuleDecl aliasDecl) {
      // TryLookupFilter works outward, looking for a match to the filter for
      // each enclosing module.
      if (!bindings.ResolveQualifiedModuleIdRootImport(aliasDecl, aliasDecl.TargetQId, out var root)) {
        //        if (!bindings.TryLookupFilter(alias.TargetQId.rootToken(), out root, m => alias != m)
        Reporter.Error(MessageSource.Resolver, aliasDecl.TargetQId.Origin, ModuleNotFoundErrorMessage(0, aliasDecl.TargetQId.Path));
      } else {
        root = SkipEnclosingModules(aliasDecl.TargetQId, root);
        aliasDecl.TargetQId.Root = root;
        declarationPointers.AddOrUpdate(root, v => aliasDecl.TargetQId.Root = v, Util.Concat);
        dependencies.AddEdge(aliasDecl, root);
      }
    } else if (moduleDecl is AbstractModuleDecl abstractDecl) {
      if (!bindings.ResolveQualifiedModuleIdRootAbstract(abstractDecl, abstractDecl.QId, out var root)) {
        //if (!bindings.TryLookupFilter(abs.QId.rootToken(), out root,
        //  m => abs != m && (((abs.EnclosingModuleDefinition == m.EnclosingModuleDefinition) && (abs.Exports.Count == 0)) || m is LiteralModuleDecl)))
        Reporter.Error(MessageSource.Resolver, abstractDecl.Origin, ModuleNotFoundErrorMessage(0, abstractDecl.QId.Path));
      } else {
        root = SkipEnclosingModules(abstractDecl.QId, root);
        abstractDecl.QId.Root = root;
        declarationPointers.AddOrUpdate(root, v => abstractDecl.QId.Root = v, Util.Concat);
        dependencies.AddEdge(abstractDecl, root);
      }
    }
  }
}
