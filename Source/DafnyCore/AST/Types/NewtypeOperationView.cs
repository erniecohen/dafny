using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>A scope-sensitive operation view; nominal compatibility still uses NominalType.</summary>
public static class NewtypeOperationView {
  public enum ViewStatus { Resolved, Undetermined, Cyclic }
  public readonly record struct View(ViewStatus Status, Type NominalType, Type BaseType,
    IReadOnlyList<TopLevelDecl> Path);

  public static View Get(Type nominal, TopLevelDecl stopAt = null, bool preserveSubsetTypes = false) {
    var path = new List<TopLevelDecl>();
    var declarations = new HashSet<TopLevelDecl>();
    var current = nominal;
    while (current != null) {
      current = current.NormalizeAndAdjustForScope();
      if (current is TypeProxy || current is UserDefinedType { ResolvedClass: null }) {
        return new View(ViewStatus.Undetermined, nominal, null, path);
      }
      if (stopAt != null && current is UserDefinedType { ResolvedClass: var declarationAtStep } && declarationAtStep == stopAt) {
        return new View(ViewStatus.Resolved, nominal, current, path);
      }
      if (preserveSubsetTypes && current is UserDefinedType { ResolvedClass: SubsetTypeDecl } &&
          SubsetHasOperationCarrier(current, stopAt)) {
        return new View(ViewStatus.Resolved, nominal, current, path);
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

  // These family queries are deliberately local to codatatype operations. They
  // do not affect nominal type compatibility or the existing global type queries.
  private static View GetCoDatatypeView(Type nominal) {
    var path = new List<TopLevelDecl>();
    var subsets = new HashSet<SubsetTypeDecl>();
    var current = nominal;
    while (true) {
      var view = Get(current);
      path.AddRange(view.Path);
      if (view.Status != ViewStatus.Resolved) {
        return new View(view.Status, nominal, null, path);
      }
      current = view.BaseType.NormalizeAndAdjustForScope();
      // The generic view may preserve subsets for arrow-family classification.
      // Here, inspect each visible constraint before instantiating its base.
      if (current is UserDefinedType { ResolvedClass: SubsetTypeDecl subset } instance) {
        if (!subsets.Add(subset) && HasDeclarationCycle(subset)) {
          return new View(ViewStatus.Cyclic, nominal, null, path);
        }
        path.Add(subset);
        current = subset.RhsWithArgumentIgnoringScope(instance.TypeArgs);
        continue;
      }
      return new View(ViewStatus.Resolved, nominal, current, path);
    }
  }

  public static Type CoDatatypeType(Type nominal, bool extendedNewtypeBases) {
    if (extendedNewtypeBases && GetCoDatatypeView(nominal).BaseType is { IsCoDatatype: true } baseType) {
      return baseType;
    }
    return nominal.NormalizeExpand();
  }

  public static bool IsCoDatatype(Type nominal, bool extendedNewtypeBases) =>
    CoDatatypeType(nominal, extendedNewtypeBases).IsCoDatatype;

  public static bool InvolvesCoDatatype(Type nominal, bool extendedNewtypeBases) =>
    extendedNewtypeBases ? GetCoDatatypeView(nominal).BaseType?.InvolvesCoDatatype ?? nominal.InvolvesCoDatatype
      : nominal.InvolvesCoDatatype;

  private static bool HasNontrivialRefinement(View view) => view.Path.Any(declaration =>
    declaration switch {
      NewtypeDecl n => n.Constraint != null && n.Constraint.Resolved is not LiteralExpr { Value: true },
      SubsetTypeDecl s => s.Constraint != null && s.Constraint.Resolved is not LiteralExpr { Value: true },
      _ => false
    });

  // A strengthened result type is an implicit postcondition. The existing
  // co-call rule must not assume it while proving that same result constraint.
  public static bool HasConstrainedCoDatatypeResult(Type nominal, bool extendedNewtypeBases) {
    if (!extendedNewtypeBases) {
      return false;
    }
    var view = GetCoDatatypeView(nominal);
    return view.BaseType?.IsCoDatatype == true && view.Path.Any(d => d is NewtypeDecl) &&
      HasNontrivialRefinement(view);
  }

  // Only representation-identity casts may preserve a constructor guard. A
  // destination predicate could destruct arbitrarily deeply, so a strengthening
  // cast is intentionally left to the existing destructive/default traversal.
  public static bool IsCoDatatypeIdentityConversion(ConversionExpr conversion, bool extendedNewtypeBases) {
    if (!extendedNewtypeBases) {
      return false;
    }
    var source = GetCoDatatypeView(conversion.E.Type);
    var destination = GetCoDatatypeView(conversion.Type);
    return source.BaseType?.IsCoDatatype == true && destination.BaseType?.IsCoDatatype == true &&
      source.BaseType.Equals(destination.BaseType, true) &&
      (source.Path.Any(d => d is NewtypeDecl) || destination.Path.Any(d => d is NewtypeDecl)) &&
      !HasNontrivialRefinement(destination);
  }

  private static bool SubsetHasOperationCarrier(Type nominal, TopLevelDecl expected) {
    var current = nominal;
    var seen = new HashSet<TopLevelDecl>();
    while (current != null) {
      current = current.NormalizeAndAdjustForScope();
      if (current is UserDefinedType { ResolvedClass: TypeSynonymDecl synonym } alias) {
        if (!seen.Add(synonym) && HasDeclarationCycle(synonym)) {
          return false;
        }
        current = synonym.RhsWithArgumentIgnoringScope(alias.TypeArgs);
        continue;
      }
      // A subset over another nominal newtype must be unwrapped further before
      // it supplies the selected operation. A subset of the carrier preserves
      // its own refined signature (notably total/no-reads arrows and nat).
      return current is not TypeProxy && current is not UserDefinedType { ResolvedClass: NewtypeDecl } &&
        (current is not UserDefinedType user || user.ResolvedClass == expected);
    }
    return false;
  }

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
