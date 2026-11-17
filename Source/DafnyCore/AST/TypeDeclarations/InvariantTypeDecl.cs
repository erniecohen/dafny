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
/// With no witness member, the type is possibly empty, as a subset type with "witness *" is. A witness member
/// "witness e" (or "ghost witness e") is a static const of type D, so its well-formedness is the ordinary check that
/// e satisfies the invariants, and the witness of this declaration is that const. The type is then auto-initializable
/// (or nonempty), and where it is compiled, the default value of D is the witness.
/// </summary>
public class InvariantTypeDecl : SubsetTypeDecl {
  public override string WhatKind => Owner.WhatKind;
  public readonly TopLevelDeclWithMembers Owner;

  public InvariantTypeDecl(TopLevelDeclWithMembers owner)
    : this(owner, NonVariantTypeParameters(owner.TypeArgs)) {
  }

  /// <summary>
  /// Unresolved clones of the owner's type parameters, each non-variant. A value of D&lt;A&gt; satisfies the invariants
  /// as instantiated with A, which need not imply them as instantiated with B, even where A is a subtype of B: an
  /// invariant may quantify over a type parameter. So D&lt;A&gt; is a subtype of D&lt;B&gt; only where A and B are the same
  /// type, whatever the variance of the owner, and a conversion between them is checked as a conversion to any
  /// subset type is. "raw D" keeps the owner's variance. Each parameter keeps its strictness, which concerns the
  /// cardinality of the type, not its subtyping.
  /// </summary>
  private static List<TypeParameter> NonVariantTypeParameters(List<TypeParameter> typeParameters) {
    var cloner = new Cloner();
    return typeParameters.ConvertAll(tp => new TypeParameter(tp.Origin, tp.NameNode,
      tp.StrictVariance ? TPVarianceSyntax.NonVariant_Strict : TPVarianceSyntax.NonVariant_Permissive,
      tp.Characteristics, tp.TypeBounds.ConvertAll(cloner.CloneType), tp.Attributes));
  }

  private InvariantTypeDecl(TopLevelDeclWithMembers owner, List<TypeParameter> tps)
    : this(owner, tps, new BoundVar(owner.Origin, "v", RawType(owner, tps))) {
  }

  private InvariantTypeDecl(TopLevelDeclWithMembers owner, List<TypeParameter> tps, BoundVar var)
    : this(owner, tps, var, owner.Members.OfType<MemberWitness>().FirstOrDefault()) {
  }

  private InvariantTypeDecl(TopLevelDeclWithMembers owner, List<TypeParameter> tps, BoundVar var, MemberWitness? witness)
    : base(owner.Origin, owner.NameNode,
      new TypeParameterCharacteristics(TypeParameter.EqualitySupportValue.Unspecified, AutoInit(witness), false),
      tps, owner.EnclosingModuleDefinition, var, Constraint(owner, var), WitnessKindOf(witness), WitnessReference(owner, tps, witness), null) {
    Owner = owner;
    if (witness is { ExplicitType: null }) {
      // As if the witness had been written "static const witness#k: D<X, ...> := e", where X, ... are the type
      // parameters of the owner.
      witness.ExplicitType = new UserDefinedType(witness.Origin, owner.Name, TypeArgumentsNamed(witness.Origin, owner.TypeArgs));
      witness.Type = witness.ExplicitType;
    }
    InheritVisibility(owner, false);
    // The base constructor made the synonym that stands for this type where it is provided but not revealed,
    // before this type took on the visibility of its owner. Make it again, so that it has that visibility too.
    this.NewSelfSynonym();
  }

  private static Type.AutoInitInfo AutoInit(MemberWitness? witness) {
    return witness == null ? Type.AutoInitInfo.MaybeEmpty
      : witness.IsGhost ? Type.AutoInitInfo.Nonempty : Type.AutoInitInfo.CompilableValue;
  }

  private static WKind WitnessKindOf(MemberWitness? witness) {
    return witness == null ? WKind.OptOut : witness.IsGhost ? WKind.Ghost : WKind.Compiled;
  }

  /// <summary>
  /// The expression "D<X, ...>.witness#k", which denotes the witness member "witness", where X, ... are the type
  /// parameters "tps" of this declaration. Like the invariants, the reserved name cannot be written in source, but
  /// name resolution finds it like any other member.
  /// </summary>
  private static Expression? WitnessReference(TopLevelDeclWithMembers owner, List<TypeParameter> tps, MemberWitness? witness) {
    if (witness == null) {
      return null;
    }
    var tok = witness.Origin;
    return new ExprDotName(tok, new NameSegment(tok, owner.Name, TypeArgumentsNamed(tok, tps)), new Name(tok, witness.Name), null);
  }

  /// <summary>
  /// The type parameters "tps", as unresolved type arguments that name them, or null if there are none.
  /// </summary>
  private static List<Type>? TypeArgumentsNamed(IOrigin tok, List<TypeParameter> tps) {
    return tps.Count == 0 ? null : tps.ConvertAll(tp => (Type)new UserDefinedType(tok, tp.Name, null));
  }

  /// <summary>
  /// Where it has a compiled witness, the type is compiled as a subset type is, with a class of its own that gives
  /// its default value, so it needs a name other than the name of its owner. A Dafny name cannot contain "#", so
  /// the sanitized result cannot be the name of another declaration. Otherwise, it is not compiled, and its values
  /// are values of the owner's target type.
  /// </summary>
  public override string GetCompileName(DafnyOptions options) {
    var name = base.GetCompileName(options); // this also determines the name of the enclosing module
    return WitnessKind == WKind.Compiled ? NonglobalVariable.SanitizeName(Owner.Name + "#") : name;
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
