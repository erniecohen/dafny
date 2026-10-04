using System.Collections.Generic;

namespace Microsoft.Dafny;

/// <summary>A scope-sensitive operation view; nominal compatibility still uses NominalType.</summary>
public static class NewtypeOperationView {
  public enum ViewStatus { Resolved, Undetermined, Cyclic }
  public readonly record struct View(ViewStatus Status, Type NominalType, Type BaseType,
    IReadOnlyList<TopLevelDecl> Path);

  public static View Get(Type nominal) {
    var path = new List<TopLevelDecl>();
    var declarations = new HashSet<TopLevelDecl>();
    var current = nominal;
    while (current != null) {
      current = current.NormalizeAndAdjustForScope();
      if (current is TypeProxy || current is UserDefinedType { ResolvedClass: null }) {
        return new View(ViewStatus.Undetermined, nominal, null, path);
      }
      if (current is UserDefinedType { ResolvedClass: TypeSynonymDecl synonym } alias) {
        if (!declarations.Add(synonym) && HasDeclarationCycle(synonym)) {
          return new View(ViewStatus.Cyclic, nominal, null, path);
        }
        path.Add(synonym);
        current = synonym.RhsWithArgumentIgnoringScope(alias.TypeArgs);
        continue;
      }
      if (current is not UserDefinedType { ResolvedClass: NewtypeDecl declaration } instance) {
        return new View(ViewStatus.Resolved, nominal, current, path);
      }
      if (!declarations.Add(declaration) && HasDeclarationCycle(declaration)) {
        return new View(ViewStatus.Cyclic, nominal, null, path);
      }
      path.Add(declaration);
      current = declaration.ConcreteBaseType(instance.TypeArgs);
    }
    return new View(ViewStatus.Undetermined, nominal, null, path);
  }

  public static bool IsOrdinal(Type nominal) => Get(nominal).BaseType?.IsBigOrdinalType == true;

  private static bool HasDeclarationCycle(TopLevelDecl root) {
    // Raw declaration dependencies distinguish an expanding declaration cycle from
    // finite repeated actual arguments, such as Id<Id<int>> when Id<T> = T.
    var pending = new Stack<Type>();
    var seen = new HashSet<TopLevelDecl>();
    pending.Push(root is NewtypeDecl n ? n.BaseType : ((TypeSynonymDecl)root).Rhs);
    while (pending.Count != 0) {
      var type = pending.Pop()?.Normalize();
      if (type == null) {
        continue;
      }
      foreach (var argument in type.TypeArgs) {
        pending.Push(argument);
      }
      if (type is UserDefinedType user) {
        if (user.ResolvedClass == root) {
          return true;
        }
        if (user.ResolvedClass != null && seen.Add(user.ResolvedClass)) {
          if (user.ResolvedClass is NewtypeDecl declaration) {
            pending.Push(declaration.BaseType);
          } else if (user.ResolvedClass is TypeSynonymDecl synonym) {
            pending.Push(synonym.Rhs);
          }
        }
      }
    }
    return false;
  }
}
