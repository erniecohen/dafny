#nullable enable
using System;
using System.Collections.Generic;

namespace Microsoft.Dafny;

/// <summary>
/// Per-resolution interning, separate from publication to the program. Factories create
/// intrinsic builtin definitions, never declarations from the requesting module. Each
/// worker records the definitions it uses in its own SystemModuleManager view.
/// </summary>
internal sealed class ResolutionBuiltinCatalog {
  private readonly object gate = new();
  private readonly SystemModuleManager factory;
  private readonly Dictionary<int, ValuetypeDecl> bitvectors = new();
  private readonly Dictionary<string, TopLevelDecl> pretypes;

  public ResolutionBuiltinCatalog(SystemModuleManager initial) {
    factory = new SystemModuleManager(initial);
    pretypes = new Dictionary<string, TopLevelDecl>(initial.PreTypeBuiltins);
  }

  public void Array(SystemModuleManager view, int dimensions, Action<SystemModuleManager> create) {
    lock (gate) {
      using var naming = new ModuleNamingScope("_System.array" + dimensions);
      create(factory);
      view.ImportArray(factory, dimensions);
    }
  }

  public void Arrow(SystemModuleManager view, int arity) {
    lock (gate) {
      using var naming = new ModuleNamingScope("_System.arrow" + arity);
      factory.CreateArrowTypeDecl(arity);
      view.ImportArrow(factory, arity);
    }
  }

  public TupleTypeDecl Tuple(SystemModuleManager view, IOrigin origin, int dimensions, bool allowCreation, List<bool>? ghostness) {
    lock (gate) {
      using var naming = new ModuleNamingScope("_System.tuple" + dimensions);
      var declaration = factory.TupleType(origin, dimensions, allowCreation, ghostness);
      view.ImportTuple(factory, declaration, origin);
      return declaration;
    }
  }

  public ValuetypeDecl Bitvector(int width, Func<ValuetypeDecl> create) {
    lock (gate) {
      if (!bitvectors.TryGetValue(width, out var declaration)) {
        using var naming = new ModuleNamingScope("_System.bv" + width);
        declaration = create();
        bitvectors.Add(width, declaration);
      }
      return declaration;
    }
  }

  public TopLevelDecl Pretype(string name, Func<TopLevelDecl> create) {
    lock (gate) {
      if (!pretypes.TryGetValue(name, out var declaration)) {
        // Includes the complete first-touch initialization of bitvector members.
        using var naming = new ModuleNamingScope("_System." + name);
        declaration = create();
        // Signature initialization can recursively ask for this same pretype;
        // the requesting resolver has already installed its provisional entry.
        pretypes[name] = declaration;
      }
      return declaration;
    }
  }
}
