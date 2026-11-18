#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// What "import [opened] X := Q[`E]" (--copy-imports) adds to the module declaration it stands for.
/// The declaration itself is the literal module "module X refines Q {}".
/// </summary>
/// <param name="Opened">The import is "opened".</param>
/// <param name="Exports">The export sets of the copy that the importing module sees; empty for the default export.</param>
/// <param name="Inherited">The declaration was not written in the module that has it: a refining module made it
/// for a copying import that it inherits.</param>
public record CopyImportInfo(bool Opened, List<IOrigin> Exports, bool Inherited = false);

/// <summary>
/// Represents module X { ... }
/// </summary>
public class LiteralModuleDecl : ModuleDecl, ICanFormat, IHasSymbolChildren {
  public ModuleDefinition ModuleDef;

  /// <summary>
  /// Non-null if the declaration is a copying import, "import X := Q".  ModuleDef then refines Q and has no
  /// declarations of its own.
  /// </summary>
  public CopyImportInfo? CopyImport;

  public override bool Opened => CopyImport is { Opened: true };

  /// <summary>
  /// For a copying import with export sets, "import X := Q`E": what the importing module sees of the copy.
  /// </summary>
  [FilledInDuringResolution] public ModuleSignature? CopyImportView;

  [FilledInDuringResolution] public ModuleSignature? DefaultExport;  // the default export set of the module.

  private ModuleSignature? emptySignature;
  public override ModuleSignature AccessibleSignature(bool ignoreExports) {
    if (ignoreExports) {
      return Signature;
    }
    return this.AccessibleSignature();
  }

  public override bool SingleFileToken => ModuleDef.SingleFileToken;

  public override ModuleSignature AccessibleSignature() {
    if (CopyImportView != null) {
      return CopyImportView;
    }
    if (DefaultExport == null) {
      if (emptySignature == null) {
        emptySignature = new ModuleSignature();
      }
      return emptySignature;
    }
    return DefaultExport;
  }

  public override IEnumerable<INode> Children => new[] { ModuleDef };
  public override IEnumerable<INode> PreResolveChildren => Children;

  public LiteralModuleDecl(Cloner cloner, LiteralModuleDecl original, ModuleDefinition enclosingModule)
    : base(cloner, original, enclosingModule) {
    var newModuleDefinition = cloner.CloneLiteralModuleDefinition ? cloner.CloneModuleDefinition(original.ModuleDef, enclosingModule) : original.ModuleDef;
    ModuleDef = newModuleDefinition;
    DefaultExport = original.DefaultExport;
    CopyImport = original.CopyImport;
    CopyImportView = original.CopyImportView;
    BodyStartTok = ModuleDef.BodyStartTok;
  }

  [SyntaxConstructor]
  public LiteralModuleDecl(DafnyOptions options, IOrigin origin, Name nameNode, Attributes? attributes,
    ModuleDefinition enclosingModuleDefinition, string cloneId, ModuleDefinition moduleDef)
    : base(options, origin, nameNode, attributes, enclosingModuleDefinition, cloneId) {
    ModuleDef = moduleDef;
    BodyStartTok = moduleDef.BodyStartTok;
    moduleDef.EnclosingLiteralModuleDecl = this;
  }

  public LiteralModuleDecl(DafnyOptions options, ModuleDefinition moduleDef, ModuleDefinition? enclosingModule, Guid cloneId)
    : base(options, moduleDef.Origin, moduleDef.NameNode, null, enclosingModule, cloneId) {
    ModuleDef = moduleDef;
    BodyStartTok = moduleDef.BodyStartTok;
    moduleDef.EnclosingLiteralModuleDecl = this;
  }

  public override object Dereference() { return ModuleDef; }
  public bool SetIndent(int indentBefore, TokenNewIndentCollector formatter) {
    var innerIndent = indentBefore + formatter.SpaceTab;
    var allTokens = ModuleDef.OwnedTokens.ToList();
    if (allTokens.Any()) {
      formatter.SetOpeningIndentedRegion(allTokens[0], indentBefore);
    }

    foreach (var token in allTokens) {
      switch (token.val) {
        case "abstract":
        case "module": {
            break;
          }
        case "{": {
            if (TokenNewIndentCollector.IsFollowedByNewline(token)) {
              formatter.SetOpeningIndentedRegion(token, indentBefore);
            } else {
              formatter.SetAlign(indentBefore, token, out innerIndent, out _);
            }

            break;
          }
        case "}": {
            formatter.SetClosingIndentedRegionAligned(token, innerIndent, indentBefore);
            break;
          }
      }
    }

    Attributes.SetIndents(Attributes, indentBefore, formatter);
    Attributes.SetIndents(ModuleDef.Attributes, indentBefore, formatter);

    foreach (var decl2 in ModuleDef.TopLevelDecls) {
      formatter.SetDeclIndentation(decl2, innerIndent);
    }

    foreach (var decl2 in ModuleDef.PrefixNamedModules) {
      formatter.SetDeclIndentation(decl2.Module, innerIndent);
    }

    return true;
  }

  public ModuleSignature Resolve(ModuleResolver resolver, CompilationData compilation) {
    // The declaration is a literal module, so it has members and such that we need
    // to resolve. First we do refinement transformation. Then we construct the signature
    // of the module. This is the public, externally visible signature. Then we add in
    // everything that the system defines, as well as any "import" (i.e. "opened" modules)
    // directives (currently not supported, but this is where we would do it.) This signature,
    // which is only used while resolving the members of the module is stored in the (basically)
    // global variable moduleInfo. Then the signatures of the module members are resolved, followed
    // by the bodies.
    var module = ModuleDef;

    var errorCount = resolver.reporter.ErrorCount;
    if (module.Implements != null) {
      var md = module.Implements.Target.ResolveTarget(resolver.reporter);
      module.Implements.Target.SetTarget(md); // If module is not found, md is null and an error message has been emitted
      if (CopyImport != null && md != null) {
        CheckCopiedModule(resolver, module.Implements.Target);
      }
    }

    var rewriters = RewriterCollection.GetRewriters(resolver.Reporter, resolver.ProgramResolver.Program);
    foreach (var rewriter in rewriters) {
      rewriter.PreResolve(module);
    }

    if (CopyImport != null) {
      // An abstract import of the copied module stands for an unknown module.  The copy's import of that
      // name stands for another unknown module, so it gets a signature of its own.
      resolver.GiveCopyItsOwnAbstractImports(module);
    }
    // The refinement transformer has declared a copy for each copying import that the module inherits.
    // Such a copy is not a declaration of the program's module graph, so it is resolved here.
    foreach (var inheritedCopy in module.TopLevelDecls.OfType<LiteralModuleDecl>()
               .Where(d => d.CopyImport is { Inherited: true } && d.Signature == null).ToList()) {
      resolver.ResolveInheritedCopy(compilation, inheritedCopy);
    }

    PipelineProfile.ModulePhase(resolver.Options, "preResolve");
    Signature = module.RegisterTopLevelDecls(resolver, true);
    Signature.Refines = module.Implements?.Target.Sig;

    var sig = Signature;
    // set up environment
    var preResolveErrorCount = resolver.reporter.ErrorCount;

    resolver.ResolveModuleExport(this, sig);
    if (CopyImport != null && module.Implements?.Target.Def is { } copiedModule) {
      if (CopyImport.Exports.Count > 0) {
        CopyImportView = resolver.CopyImportViewOf(this, copiedModule.Name);
      } else if (DefaultExport == null && !CopyImport.Inherited) {
        // as for "import X = Q"
        resolver.reporter.Error(MessageSource.Resolver, module.Implements.Target.RootToken(),
          "no default export set declared in module: {0}", copiedModule.Name);
      }
    }
    PipelineProfile.ModulePhase(resolver.Options, "registrationAndExports");
    var good = module.Resolve(sig, resolver);

    if (good && resolver.reporter.ErrorCount == preResolveErrorCount) {
      // Check that the module export gives a self-contained view of the module.
      resolver.CheckModuleExportConsistency(compilation, module);
    }

    var tempVis = new VisibilityScope();
    tempVis.Augment(sig.VisibilityScope);
    tempVis.Augment(resolver.ProgramResolver.SystemModuleManager.systemNameInfo.VisibilityScope);
    Type.PushScope(tempVis);

    foreach (var rewriter in rewriters) {
      if (!good || resolver.reporter.ErrorCount != preResolveErrorCount) {
        break;
      }

      rewriter.PostResolveIntermediate(module);
    }

    if (good && resolver.reporter.ErrorCount == errorCount) {
      module.SuccessfullyResolved = true;
    }

    Type.PopScope(tempVis);

    PipelineProfile.ModulePhase(resolver.Options, "exportsAndIntermediate");
    if (!good || resolver.reporter.HasErrors) {
      return sig;
    }

    Type.PushScope(tempVis);
    var phases = PipelineProfile.RecordingModulePhases;
    resolver.ComputeIsRecursiveBit(compilation, module, rewriters);
    PipelineProfile.ModulePhase(resolver.Options, "isRecursive");
    resolver.FillInDecreasesClauses(module);
    foreach (var iter in module.TopLevelDecls.OfType<IteratorDecl>()) {
      resolver.reporter.Info(MessageSource.Resolver, iter.Origin, Printer.IteratorClassToString(resolver.Reporter.Options, iter));
    }
    PipelineProfile.ModulePhase(resolver.Options, "decreases");

    foreach (var rewriter in rewriters) {
      rewriter.PostDecreasesResolve(module);
      if (phases) {
        PipelineProfile.ModulePhase(resolver.Options, "postDecreasesResolve:" + rewriter.GetType().Name);
      }
    }

    resolver.FillInAdditionalInformation(module);
    FuelAdjustment.CheckForFuelAdjustments(resolver.reporter, module);
    PipelineProfile.ModulePhase(resolver.Options, "additionalInformation");

    foreach (var rewriter in rewriters) {
      rewriter.PostResolve(module);
      if (phases) {
        PipelineProfile.ModulePhase(resolver.Options, "postResolve:" + rewriter.GetType().Name);
      }
    }

    Type.PopScope(tempVis);
    return sig;
  }

  /// <summary>
  /// Says whether "copied" has an export set, other than its default one, with the name of a copy of it.
  /// </summary>
  public static bool HasExportSetNamedLikeCopy(ModuleDefinition copied, string copyName) {
    return copied.TopLevelDecls.OfType<ModuleExportDecl>().Any(export => !export.IsDefault && export.Name == copyName);
  }

  /// <summary>
  /// Checks that the module that this copying import names can be copied, and makes the copy abstract
  /// if that module is abstract.
  /// </summary>
  private void CheckCopiedModule(ModuleResolver resolver, ModuleQualifiedId target) {
    var copied = target.Def;
    if (copied.ModuleKind == ModuleKindEnum.Abstract) {
      ModuleDef.ModuleKind = ModuleKindEnum.Abstract;
    }
    // The default export set of a module has the module's name, so the one of the copy has the copy's name.
    if (!CopyImport!.Inherited && HasExportSetNamedLikeCopy(copied, Name)) {
      resolver.reporter.Error(MessageSource.Resolver, Origin,
        $"the copying import '{Name}' cannot copy module '{target}', because that module has an export set named '{Name}', which is the name of the default export set of the copy");
    }
    if (resolver.Options.DisallowExterns) {
      return;
    }

    // A declaration with an {:extern} attribute names one thing of the target program.  A copy of the
    // declaration would be a second Dafny declaration of that one thing.
    string? externDeclaration = null;
    if (Attributes.Contains(copied.Attributes, "extern")) {
      externDeclaration = "the module";
    } else {
      foreach (var declaration in copied.TopLevelDecls) {
        if (declaration is not ModuleDecl && Attributes.Contains(declaration.Attributes, "extern")) {
          externDeclaration = $"{declaration.WhatKind} '{declaration.Name}'";
        } else if (declaration is TopLevelDeclWithMembers withMembers &&
                   withMembers.Members.Find(member => Attributes.Contains(member.Attributes, "extern")) is { } externMember) {
          externDeclaration = $"{externMember.WhatKind} '{externMember.Name}'";
        }
        if (externDeclaration != null) {
          break;
        }
      }
    }
    if (externDeclaration != null) {
      var where = CopyImport!.Inherited ? new NestedOrigin(ModuleDef.EnclosingModule.Origin, target.Origin, "inherited copying import") : target.Origin;
      resolver.reporter.Error(MessageSource.Resolver, where,
        $"a copying import cannot copy module '{target}', because {externDeclaration} has an {{:extern}} attribute");
    }
  }

  public void BindModuleNames(ProgramResolver resolver, ModuleBindings parentBindings) {
    Contract.Requires(this != null);

    var bindings = ModuleDef.BindModuleNames(resolver, parentBindings);
    if (!parentBindings.BindName(Name, this, bindings)) {
      parentBindings.TryLookup(Name, out var otherModule);
      resolver.Reporter.Error(MessageSource.Resolver, new NestedOrigin(Origin, otherModule.Origin), "Duplicate module name: {0}", Name);
    }
  }

  public IEnumerable<ISymbol> ChildSymbols => ModuleDef.TopLevelDecls.SelectMany(decl => {
    if (decl is DefaultClassDecl defaultClassDecl) {
      return defaultClassDecl.Members;
    }

    if (decl is ISymbol symbol) {
      return new[] { symbol };
    }

    return Enumerable.Empty<ISymbol>();
  });
}