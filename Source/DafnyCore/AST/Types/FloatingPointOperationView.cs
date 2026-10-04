using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>Visible floating fields in an instantiated compiled value shape.</summary>
public static class FloatingPointOperationView {
  private sealed class Dependencies {
    public bool ContainsFloat;
    public readonly HashSet<TypeParameter> Parameters = [];

    public bool Include(Dependencies other) {
      var changed = !ContainsFloat && other.ContainsFloat;
      ContainsFloat |= other.ContainsFloat;
      var count = Parameters.Count;
      Parameters.UnionWith(other.Parameters);
      return changed || count != Parameters.Count;
    }
  }

  public static bool Contains(Type nominal, bool fp32) {
    // Compute a finite summary for each visible declaration: a floating constant, and
    // the parameters that can occur in its non-ghost constructor fields.
    // A declaration-only visited guard misses Box<Box<fp32>>; unrestricted
    // instantiated expansion fails to terminate on D<T> -> D<seq<T>>.
    var summaries = new Dictionary<TopLevelDecl, Dependencies>();

    Dependencies Visit(Type type) {
      var result = new Dependencies();
      type = type.NormalizeAndAdjustForScope();
      if (fp32 ? type is Fp32Type : type is Fp64Type) {
        result.ContainsFloat = true;
      } else if (type is UserDefinedType { ResolvedClass: TypeParameter parameter }) {
        result.Parameters.Add(parameter);
      } else if (type is MapType map) {
        result.Include(Visit(map.Domain));
        result.Include(Visit(map.Range));
      } else if (type is CollectionType collection) {
        result.Include(Visit(collection.Arg));
      } else if (type is UserDefinedType instance &&
                 instance.ResolvedClass is NewtypeDecl or TypeSynonymDecl or DatatypeDecl) {
        var declaration = instance.ResolvedClass;
        if (!summaries.TryGetValue(declaration, out var summary)) {
          summary = new Dependencies();
          summaries.Add(declaration, summary);
        }
        result.ContainsFloat = summary.ContainsFloat;
        for (var i = 0; i < declaration.TypeArgs.Count; i++) {
          if (summary.Parameters.Contains(declaration.TypeArgs[i])) {
            result.Include(Visit(instance.TypeArgs[i]));
          }
        }
      }
      return result;
    }

    bool changed;
    do {
      changed = false;
      var count = summaries.Count;
      Visit(nominal);
      foreach (var (declaration, summary) in summaries.ToList()) {
        if (declaration is NewtypeDecl newtype) {
          changed |= summary.Include(Visit(newtype.BaseType));
        } else if (declaration is TypeSynonymDecl synonym) {
          changed |= summary.Include(Visit(synonym.Rhs));
        } else if (declaration is DatatypeDecl datatype) {
          foreach (var formal in datatype.Ctors.SelectMany(ctor => ctor.Formals).Where(formal => !formal.IsGhost)) {
            changed |= summary.Include(Visit(formal.Type));
          }
        }
      }
      changed |= summaries.Count != count;
    } while (changed);
    return Visit(nominal).ContainsFloat;
  }
}
