using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Microsoft.Dafny {
  /// <summary>
  /// Issue 105 (--member-invariants): the invariants that a refining module adds (section 4.2 of
  /// docs/dev/issue105-member-invariants-plan.md).
  ///
  /// A refining module "M1" may add invariants to a type "T" of its refinement base "M0", by declaring them in the
  /// refining declaration, or in a trait that "T" extends, or by giving "T" a parent that has invariants.  "M1.T" is
  /// then a different type from "M0.T", with more conjuncts.  An assert at an inherited origin becomes an assume,
  /// because the base made it; for a conversion to "M1.T" that is sound only for the conjuncts the base checked at the
  /// same site.  So at an inherited site, each conjunct that the base did not check is checked, and the others are
  /// assumed.  The pipeline verifies, of a copy, only the checks its module must make itself
  /// (CanVerifyOrigin.IsReverifiedInheritedCheck); a copy with such a check is marked when its implementation is
  /// added.  A member that an added invariant made raw is verified again in full, since its "this" no longer
  /// satisfies the invariants that the base's proof of it may have used.
  /// </summary>
  public partial class BoogieGenerator {
    /// <summary>
    /// The number of checks, in the implementation being translated, of conjuncts that the refinement base did not
    /// check at an inherited site.  It is reset when the implementation is added.
    /// </summary>
    private int addedInvariantChecks;

    /// <summary>
    /// The origin of the check, at "origin", of the conjunct "invariant" of a conversion to the derived declaration of
    /// "owner".  At an inherited site, a conjunct that the refinement base did not check there is checked.
    /// </summary>
    private IOrigin InheritedConjunctOrigin(IOrigin origin, TopLevelDeclWithMembers owner, MemberInvariant invariant) {
      if (!origin.IsInherited(currentModule) || MustReverifyInheritedCode ||
          CheckedByRefinementBase(owner, invariant, currentModule, options)) {
        return origin;
      }
      addedInvariantChecks++;
      return new ForceCheckOrigin(origin);
    }

    /// <summary>
    /// The origin of a check, at "origin", of membership in "targetType" that is not split into the conjuncts of the
    /// types with invariants it involves, such as the conversion of a sequence of raw values to a sequence of finished
    /// ones.  At an inherited site, it is checked if one of those types has a conjunct the base did not check.
    /// </summary>
    private IOrigin InheritedMembershipOrigin(IOrigin origin, Type targetType) {
      if (!origin.IsInherited(currentModule) || MustReverifyInheritedCode ||
          !InvariantOwners(targetType, []).Any(owner =>
            ConstraintInvariants(owner).Any(invariant => !CheckedByRefinementBase(owner, invariant, currentModule, options)))) {
        return origin;
      }
      addedInvariantChecks++;
      return new ForceCheckOrigin(origin);
    }

    /// <summary>
    /// The declarations with invariants whose constraints membership in "type" implies: those of the derived
    /// declarations it names, of its type arguments, and of the base types of the subset types and newtypes it names.
    /// </summary>
    private static IEnumerable<TopLevelDeclWithMembers> InvariantOwners(Type type, HashSet<TopLevelDecl> visited) {
      if (type.NormalizeExpandKeepConstraints() is not { } normalized) {
        yield break;
      }
      if (normalized is UserDefinedType { ResolvedClass: { } resolved } && visited.Add(resolved)) {
        switch (resolved) {
          case InvariantTypeDecl invariantTypeDecl:
            yield return invariantTypeDecl.Owner;
            break;
          case NonNullTypeDecl { Class.InvariantTypeDecl: { } classInvariants } and not RawNonNullTypeDecl:
            yield return classInvariants.Owner;
            break;
          case SubsetTypeDecl { Var: { } var }:
            foreach (var owner in InvariantOwners(var.Type, visited)) { yield return owner; }
            break;
          case NewtypeDecl { BaseType: { } baseType }:
            foreach (var owner in InvariantOwners(baseType, visited)) { yield return owner; }
            break;
        }
      }
      foreach (var argument in normalized.TypeArgs) {
        foreach (var owner in InvariantOwners(argument, visited)) { yield return owner; }
      }
    }

    /// <summary>
    /// The invariants whose applications the constraint of the derived declaration of "owner" conjoins: the inherited
    /// ones, then its own.
    /// </summary>
    private static IEnumerable<MemberInvariant> ConstraintInvariants(TopLevelDeclWithMembers owner) {
      return owner.InvariantTypeDecl == null ? []
        : owner.InvariantTypeDecl.InheritedInvariants.Select(inherited => inherited.Invariant)
          .Concat(InvariantTypeDecl.Invariants(owner));
    }

    /// <summary>
    /// Whether the refinement base of "module" checked the conjunct "invariant" of a conversion to the derived
    /// declaration of "owner" at the base's counterpart of an inherited site of "module".
    ///
    /// The base converted to the derived declaration of BaseOwner (see there).  Invariant numbers are stable under
    /// refinement: a refining declaration numbers its own invariants after the inherited ones.  So the base checked
    /// "invariant" if the constraint of BaseOwner conjoins an invariant with the same number, declared by a type
    /// that "invariant"'s type refines, directly or not.
    /// </summary>
    private static bool CheckedByRefinementBase(TopLevelDeclWithMembers owner, MemberInvariant invariant,
      ModuleDefinition module, DafnyOptions options) {
      var baseOwner = BaseOwner(owner, module, options);
      if (baseOwner == owner) {
        return true;
      }
      if (baseOwner is not TopLevelDeclWithMembers { InvariantTypeDecl: not null } withInvariants) {
        return false;
      }
      var lineage = Lineage(invariant.EnclosingClass);
      return ConstraintInvariants(withInvariants).Any(checkedInvariant =>
        checkedInvariant.Number == invariant.Number && lineage.Contains(Canonical(checkedInvariant.EnclosingClass)));
    }

    /// <summary>
    /// The declaration to whose derived declaration the refinement base of "module" converted, at an inherited site
    /// of "module" that converts to that of "owner", or null if there is none:
    ///  - for a declaration of "module", the declaration of the base that it refines or copies;
    ///  - for any other declaration, "owner" itself, since "module" shares it with its base, unless "module" binds an
    ///    abstract import of its base to a module, or rebinds a copying import of its base to a copy of a refinement
    ///    of the copied module (https://github.com/erniecohen/dafny/issues/107).  The base then converted to the
    ///    declarations of the module that the import names, or of its own copy, which the bound module refines, so
    ///    the first declaration that "owner" refines is taken, and with it only the conjuncts of the original.  That
    ///    is conservative, and the hidden --legacy-unchecked-import-binding, which allows a binding that is not a
    ///    refinement, takes none.
    /// </summary>
    private static TopLevelDecl BaseOwner(TopLevelDeclWithMembers owner, ModuleDefinition module, DafnyOptions options) {
      if (owner.EnclosingModuleDefinition == module) {
        return owner.CardinalityRefinementBase;
      }
      if (!BindsAbstractImport(module)) {
        return owner;
      }
      if (options.Get(CommonOptionBag.LegacyUncheckedImportBinding)) {
        return null;
      }
      TopLevelDecl root = owner;
      var seen = new HashSet<TopLevelDecl>();
      while (RefinedDeclaration(root) is { } refined && seen.Add(root)) {
        root = refined;
      }
      return root;
    }

    /// <summary>
    /// Whether "module" binds an abstract import of its refinement base to a module, or rebinds a copying import of
    /// its refinement base.  A rebinding copies a refinement of the module that the base copied, which may add
    /// invariants to the types of the copy.
    /// </summary>
    private static bool BindsAbstractImport(ModuleDefinition module) {
      return module.Implements != null && module.TopLevelDecls.OfType<ModuleDecl>().Any(decl =>
        (decl.CardinalityRefinementBase is AbstractModuleDecl || RebindsCopyingImport(decl, module)) &&
        !decl.Origin.IsInherited(module));
    }

    /// <summary>
    /// Whether "decl", a declaration of "module", is a copying import written in "module" in the place of a copying
    /// import that "module" inherits.  A copy that "module" makes of an inherited one is marked Inherited.
    /// </summary>
    private static bool RebindsCopyingImport(ModuleDecl decl, ModuleDefinition module) {
      return decl is LiteralModuleDecl { CopyImport: { Inherited: false } } &&
             module.Implements?.Target.Def?.TopLevelDecls.Any(original =>
               original.Name == decl.Name && original is LiteralModuleDecl { CopyImport: not null }) == true;
    }

    /// <summary>
    /// "declaration" and the declarations it refines, directly or not, each as Canonical.
    /// </summary>
    private static HashSet<TopLevelDecl> Lineage(TopLevelDecl declaration) {
      var lineage = new HashSet<TopLevelDecl>();
      for (var current = declaration; current != null && lineage.Add(Canonical(current));) {
        current = RefinedDeclaration(current);
      }
      return lineage;
    }

    /// <summary>
    /// The declaration of a refinement base that "declaration", or the declaration it stands for, refines or copies.
    /// </summary>
    private static TopLevelDecl RefinedDeclaration(TopLevelDecl declaration) {
      return declaration.CardinalityRefinementBase ?? Canonical(declaration).CardinalityRefinementBase;
    }

    /// <summary>
    /// The declaration that "declaration" stands for: the original of a declaration that a facade or an abstract
    /// signature copied, and the class of a non-null type.
    /// </summary>
    private static TopLevelDecl Canonical(TopLevelDecl declaration) {
      var seen = new HashSet<TopLevelDecl>();
      while (seen.Add(declaration)) {
        var next = declaration switch {
          NonNullTypeDecl nonNull => nonNull.Class,
          { CardinalityViewOf: { } view } => view,
          _ => null
        };
        if (next == null) {
          break;
        }
        declaration = next;
      }
      return declaration;
    }

    /// <summary>
    /// Whether "member", a copy from the refinement base, is a raw member here although its counterpart in the base
    /// is not, because an invariant that the refining module added applies it to "this".  Its "this" no longer
    /// satisfies the invariants that the base's proof of it may have used, so it is verified again in full.
    /// </summary>
    public static bool BecameRaw(MemberDecl member) {
      member = RawMemberInference.Member(member as IASTVisitorContext) ?? member;
      if (member is MemberInvariant || !RawMemberInference.IsRawMember(member) ||
          member.EnclosingClass?.EnclosingModuleDefinition is not { } module || !member.Origin.IsInherited(module)) {
        return false;
      }
      return RefinementBaseCounterpart(member) is not { } counterpart || !RawMemberInference.IsRawMember(counterpart);
    }

    /// <summary>
    /// Whether the refining module that copied "declaration" from its refinement base must verify some check of it
    /// itself, because the module adds invariants to a type that the copy may convert to (AddsInvariantConjuncts).
    /// </summary>
    public static bool MustCheckAddedInvariants(Declaration declaration, DafnyOptions options) {
      if (!options.Get(CommonOptionBag.MemberInvariants)) {
        return false;
      }
      var module = EnclosingModule(declaration);
      return module != null && declaration.Origin.IsInherited(module) && AddsInvariantConjuncts(module, options);
    }

    private static ModuleDefinition EnclosingModule(Declaration declaration) {
      return declaration switch {
        MemberDecl member => member.EnclosingClass?.EnclosingModuleDefinition,
        DatatypeCtor ctor => ctor.EnclosingDatatype?.EnclosingModuleDefinition,
        TopLevelDecl topLevelDecl => topLevelDecl.EnclosingModuleDefinition,
        _ => null
      };
    }

    /// <summary>
    /// Whether "declaration" is a copy that its module must check for the invariants it adds
    /// (MustCheckAddedInvariants), and so is in the verification scope with that module, when it comes from a
    /// refinement base outside the scope, such as a library.
    /// </summary>
    private bool AddedInvariantsInVerificationScope(Declaration declaration) {
      // The visibility is tested first; see BodyInVerificationScope.
      return declaration.IsVisibleInScope(verificationScope) && EnclosingModule(declaration) is { IsFacade: false } module &&
             module.ShouldVerify(program.Compilation) && MustCheckAddedInvariants(declaration, options);
    }

    /// <summary>
    /// Whether "module" refines a module and may convert, at an inherited site, to a type with a conjunct that its
    /// base did not check there: it declares a type with such a conjunct, or a member that became raw, or it binds an
    /// abstract import.
    /// </summary>
    public static bool AddsInvariantConjuncts(ModuleDefinition module, DafnyOptions options) {
      return AddsInvariantConjunctsCache.GetValue(module,
        m => new StrongBox<bool>(ComputeAddsInvariantConjuncts(m, options))).Value;
    }

    private static readonly ConditionalWeakTable<ModuleDefinition, StrongBox<bool>> AddsInvariantConjunctsCache = new();

    private static bool ComputeAddsInvariantConjuncts(ModuleDefinition module, DafnyOptions options) {
      if (module.Implements == null) {
        return false;
      }
      if (BindsAbstractImport(module)) {
        return true;
      }
      return module.TopLevelDecls.OfType<TopLevelDeclWithMembers>().Any(decl =>
        ConstraintInvariants(decl).Any(invariant => !CheckedByRefinementBase(decl, invariant, module, options)) ||
        decl.Members.Any(BecameRaw));
    }
  }
}
