#nullable enable
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.Dafny;

/// <summary>
/// The type of the values of a declaration that has member invariants (issue 105, under --member-invariants).
///
/// A declaration D with invariants denotes the raw type, "raw D", whose values need not satisfy the invariants.
/// The name D, written as a type, denotes this derived declaration instead: a subset type of "raw D" whose
/// constraint is the conjunction of the invariants, { d: raw D | d.invariant#0() &amp;&amp; ... }. This follows the
/// precedent of NonNullTypeDecl, where a class declaration C denotes "C?" and C is a derived subset type.
///
/// Unlike a NonNullTypeDecl, the derived declaration is not registered under a name of its own. The name D is
/// registered for the declaration itself (so that constructors, static members and exports work as before),
/// and the resolver maps D, written as a type, to this declaration. A derived declaration shares its visibility
/// with the declaration it is derived from.
///
/// With no witness member, the type is possibly empty, as a subset type with "witness *" is.
/// </summary>
public class InvariantTypeDecl : SubsetTypeDecl {
  public override string WhatKind => Owner.WhatKind;
  public readonly TopLevelDeclWithMembers Owner;

  public InvariantTypeDecl(TopLevelDeclWithMembers owner)
    : this(owner, TypeParameter.CloneTypeParameters(owner.TypeArgs)) {
  }

  private InvariantTypeDecl(TopLevelDeclWithMembers owner, List<TypeParameter> tps)
    : this(owner, tps, new BoundVar(owner.Origin, "v", RawType(owner, tps))) {
  }

  private InvariantTypeDecl(TopLevelDeclWithMembers owner, List<TypeParameter> tps, BoundVar var)
    : base(owner.Origin, owner.NameNode,
      new TypeParameterCharacteristics(TypeParameter.EqualitySupportValue.Unspecified, Type.AutoInitInfo.MaybeEmpty, false),
      tps, owner.EnclosingModuleDefinition, var, Constraint(owner, var), WKind.OptOut, null, null) {
    Owner = owner;
    InheritVisibility(owner, false);
    // The base constructor made the synonym that stands for this type where it is provided but not revealed,
    // before this type took on the visibility of its owner. Make it again, so that it has that visibility too.
    this.NewSelfSynonym();
  }

  private static UserDefinedType RawType(TopLevelDeclWithMembers owner, List<TypeParameter> tps) {
    var typeArgs = tps.ConvertAll(tp => (Type)new UserDefinedType(tp));
    return new UserDefinedType(owner.Origin, owner.Name, owner, typeArgs);
  }

  /// <summary>
  /// The conjunction of the invariants, applied to "var". The reserved names of the invariants cannot be written
  /// in source, but name resolution finds them like any other member.
  /// </summary>
  private static Expression Constraint(TopLevelDeclWithMembers owner, BoundVar var) {
    Expression? constraint = null;
    foreach (var invariant in Invariants(owner)) {
      var tok = invariant.Origin;
      Expression call = new ApplySuffix(tok, null,
        new ExprDotName(tok, new IdentifierExpr(tok, var), new Name(tok, invariant.Name), null),
        [], Token.NoToken);
      constraint = constraint == null ? call : new BinaryExpr(tok, BinaryExpr.Opcode.And, constraint, call);
    }
    Contract.Assert(constraint != null); // see Applies
    return constraint;
  }

  /// <summary>
  /// The invariants of "decl", in the order of their numbers.
  /// </summary>
  public static IEnumerable<MemberInvariant> Invariants(TopLevelDeclWithMembers decl) {
    return decl.Members.OfType<MemberInvariant>().OrderBy(invariant => invariant.Number);
  }

  /// <summary>
  /// The declarations that get a derived InvariantTypeDecl. In this stage, an inductive datatype that declares an
  /// invariant (in itself or, by refinement, in a declaration it refines).
  /// </summary>
  public static bool Applies(TopLevelDeclWithMembers decl) {
    return decl is IndDatatypeDecl && Invariants(decl).Any();
  }

  /// <summary>
  /// The type "raw D" for the declaration D with type arguments "typeArgs".
  /// </summary>
  public UserDefinedType RawType(IOrigin tok, List<Type> typeArgs) {
    return new UserDefinedType(tok, Owner.Name, Owner, typeArgs);
  }

  public override SymbolKind? Kind => Owner.Kind;

  public override string GetDescription(DafnyOptions options) {
    return Owner.GetDescription(options);
  }
}
