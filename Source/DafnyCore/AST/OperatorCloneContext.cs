using System;
using System.Collections.Generic;

namespace Microsoft.Dafny;

// This map is deliberately independent of Cloner's reference caches. In particular,
// CloneMember(m, true) cannot register an old object as a final definition clone.
public sealed class OperatorCloneContext {
  private readonly Dictionary<Declaration, Declaration> finalDeclarations = new();
  private readonly HashSet<Declaration> required = new();
  public bool HasSelections { get; set; }
  public VisibilityScope ExportScope { get; set; }
  public ModuleDefinition SystemModule { get; set; }

  public void Register(Declaration source, Declaration target) {
    finalDeclarations[source] = target;
  }

  public void Require(Declaration source) {
    required.Add(source);
  }

  public T Map<T>(T source) where T : Declaration {
    var target = finalDeclarations.GetValueOrDefault(source);
    if (target == null && required.Contains(source)) {
      throw new InvalidOperationException("operator selection has no final declaration correspondence");
    }
    // Type parameters are binders, not exported declarations. Compiler-owned type
    // heads keep their shared identities rather than passing through an export facade.
    if (ExportScope != null && source is not TypeParameter &&
        !(source is TopLevelDecl top && ReferenceEquals(top.EnclosingModuleDefinition, SystemModule))) {
      if (!source.IsVisibleInScope(ExportScope) ||
          source is TopLevelDeclWithMembers and not DefaultClassDecl && !source.IsRevealedInScope(ExportScope)) {
        throw new InvalidOperationException("operator selection cannot be transported into this export view");
      }
    }
    if (target != null && target is not T) {
      throw new InvalidOperationException("operator selection changed declaration kind");
    }
    return target == null ? source : (T)target;
  }
}
