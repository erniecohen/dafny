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
/// For a class C, the class declaration denotes "raw C?", and this derived declaration is the type "C?" that the
/// name "C?" denotes: { v: raw C? | v != null ==> v.invariant#0() &amp;&amp; ... }. Its parents are those of the class:
/// "object?" and the parent traits. The non-null type C is based on it, and "raw C" is a RawNonNullTypeDecl based
/// on the class declaration. A class type contains null, so it is auto-initializable and needs no witness.
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
      new TypeParameterCharacteristics(TypeParameter.EqualitySupportValue.Unspecified, AutoInit(owner, witness), false),
      tps, owner.EnclosingModuleDefinition, var, OwnConstraintOf(owner, var), WitnessKindOf(owner, witness), WitnessReference(owner, tps, witness), null) {
    Owner = owner;
    OwnConstraint = Constraint;
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

  private static Type.AutoInitInfo AutoInit(TopLevelDeclWithMembers owner, MemberWitness? witness) {
    return IsReferenceType(owner) ? Type.AutoInitInfo.CompilableValue
      : witness == null ? Type.AutoInitInfo.MaybeEmpty
      : witness.IsGhost ? Type.AutoInitInfo.Nonempty : Type.AutoInitInfo.CompilableValue;
  }

  /// <summary>
  /// A reference type has the witness null, which satisfies the constraint, as the base type's default value does.
  /// </summary>
  private static WKind WitnessKindOf(TopLevelDeclWithMembers owner, MemberWitness? witness) {
    return IsReferenceType(owner) ? WKind.CompiledZero
      : witness == null ? WKind.OptOut : witness.IsGhost ? WKind.Ghost : WKind.Compiled;
  }

  /// <summary>
  /// Whether "owner" is a class or a reference trait, whose derived declaration is the possibly-null type "C?".
  /// </summary>
  public static bool IsReferenceType(TopLevelDeclWithMembers owner) {
    return owner is ClassLikeDecl { NonNullTypeDecl: not null };
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
    return UserDefinedType.FromTopLevelDecl(owner.Origin, owner, typeArgs);
  }

  /// <summary>
  /// The conjunction of the invariants that "owner" declares, applied to "var", and for a reference type, "var != null
  /// ==>" that. The reserved names of the invariants cannot be written in source, but name resolution finds them like
  /// any other member. A declaration that declares no invariant but inherits some has the constraint "true" until
  /// SetInheritedInvariants gives it the inherited ones.
  /// </summary>
  private static Expression OwnConstraintOf(TopLevelDeclWithMembers owner, BoundVar var) {
    return Guarded(owner, var, Invariants(owner, var) ?? new LiteralExpr(owner.Origin, true));
  }

  private static Expression Guarded(TopLevelDeclWithMembers owner, BoundVar var, Expression invariants) {
    if (!IsReferenceType(owner)) {
      return invariants;
    }
    var tok = owner.Origin;
    return new BinaryExpr(tok, BinaryExpr.Opcode.Imp,
      new BinaryExpr(tok, BinaryExpr.Opcode.Neq, new IdentifierExpr(tok, var), new LiteralExpr(tok)), invariants);
  }

  private static Expression? Invariants(TopLevelDeclWithMembers owner, BoundVar var) {
    Expression? constraint = null;
    foreach (var invariant in Invariants(owner)) {
      constraint = Conjoin(constraint, Application(invariant, new IdentifierExpr(invariant.Origin, var)));
    }
    return constraint;
  }

  private static Expression Conjoin(Expression? conjunction, Expression conjunct) {
    return conjunction == null ? conjunct : new BinaryExpr(conjunct.Origin, BinaryExpr.Opcode.And, conjunction, conjunct);
  }

  /// <summary>
  /// The application of "invariant" to "receiver", unresolved.
  /// </summary>
  private static Expression Application(MemberInvariant invariant, Expression receiver) {
    var tok = invariant.Origin;
    return new ApplySuffix(tok, null, new ExprDotName(tok, receiver, new Name(tok, invariant.Name), null), [], Token.NoToken);
  }

  /// <summary>
  /// The constraint as the owner's own invariants give it, before SetInheritedInvariants.
  /// </summary>
  private readonly Expression OwnConstraint;

  /// <summary>
  /// The invariants that the owner inherits from the traits it extends, directly or not, in the order in which the
  /// constraint conjoins them, each with the trait that declares it as the raw type of a receiver ("raw P", or for a
  /// general trait, "raw G"), instantiated in terms of the owner's type parameters. Empty until the extends clauses are
  /// resolved (see SetInheritedInvariants).
  /// </summary>
  public List<(MemberInvariant Invariant, UserDefinedType Receiver)> InheritedInvariants { get; private set; } = [];

  /// <summary>
  /// Set the inherited invariants (see InheritedInvariants), and with them the constraint: the inherited invariants,
  /// each "(v as raw P&lt;A&gt;).invariant#k()", followed by those the owner declares, and for a reference type, guarded
  /// by "v != null". The receiver types are pre-resolved, so name resolution finds each invariant in the trait that
  /// declares it, even where the owner declares an invariant with the same reserved name.
  /// </summary>
  public void SetInheritedInvariants(List<(MemberInvariant Invariant, UserDefinedType Receiver)> inherited) {
    InheritedInvariants = inherited;
    if (inherited.Count == 0) {
      Constraint = OwnConstraint;
      return;
    }
    var subst = TypeParameter.SubstitutionMap(Owner.TypeArgs, TypeArgs.ConvertAll(tp => (Type)new UserDefinedType(tp)));
    Expression? conjunction = null;
    foreach (var (invariant, receiver) in inherited) {
      var tok = invariant.Origin;
      var receiverType = (UserDefinedType)receiver.Subst(subst);
      conjunction = Conjoin(conjunction, Application(invariant, new ConversionExpr(tok, new IdentifierExpr(tok, Var), receiverType)));
    }
    var own = Invariants(Owner, Var);
    Constraint = Guarded(Owner, Var, own == null ? conjunction! : Conjoin(conjunction, own));
  }

  /// <summary>
  /// The invariants that come before "invariant" in the constraint of the type that declares it, resolved and applied
  /// to "this": those the type inherits, each "(this as raw P&lt;A&gt;).invariant#k()", then its own with smaller numbers.
  /// The well-formedness check of "invariant" assumes them, as that of a requires clause assumes the clauses before it.
  /// </summary>
  public static IEnumerable<Expression> EarlierInvariants(MemberInvariant invariant) {
    var owner = (TopLevelDeclWithMembers)invariant.EnclosingClass;
    if (owner.InvariantTypeDecl is not { } invariantTypeDecl) {
      yield break;
    }
    var tok = invariant.Origin;
    var typeArgs = owner.TypeArgs.ConvertAll(tp => (Type)new UserDefinedType(tp));
    var thisExpr = new ThisExpr(tok) { Type = invariantTypeDecl.RawThisType(tok, typeArgs) };
    foreach (var (inherited, receiver) in invariantTypeDecl.InheritedInvariants) {
      yield return ResolvedApplication(inherited, new ConversionExpr(tok, thisExpr, receiver) { Type = receiver }, receiver.TypeArgs);
    }
    foreach (var earlier in Invariants(owner).TakeWhile(other => other.Number < invariant.Number)) {
      yield return ResolvedApplication(earlier, thisExpr, typeArgs);
    }
  }

  private static FunctionCallExpr ResolvedApplication(MemberInvariant invariant, Expression receiver, List<Type> typeArgs) {
    return new FunctionCallExpr(receiver.Origin, invariant.NameNode, receiver, receiver.Origin, Token.NoToken, new List<Expression>()) {
      Function = invariant,
      Type = Type.Bool,
      TypeApplication_AtEnclosingClass = typeArgs,
      TypeApplication_JustFunction = []
    };
  }

  /// <summary>
  /// The invariants of "decl", in the order of their numbers.
  /// </summary>
  public static IEnumerable<MemberInvariant> Invariants(TopLevelDeclWithMembers decl) {
    return decl.Members.OfType<MemberInvariant>().OrderBy(invariant => invariant.Number);
  }

  /// <summary>
  /// The declarations that get a derived InvariantTypeDecl when their names are registered: an inductive datatype, a
  /// class or a trait that declares an invariant (in itself or, by refinement, in a declaration it refines). One that
  /// declares none, but extends a trait that has invariants, gets one once its extends clauses are resolved (see
  /// ModuleResolver.CreateInheritedInvariantTypeDecls).
  /// </summary>
  public static bool Applies(TopLevelDeclWithMembers decl) {
    return MayHaveInvariants(decl) && Invariants(decl).Any();
  }

  /// <summary>
  /// The kinds of declaration whose invariants, declared or inherited, are supported.
  /// </summary>
  public static bool MayHaveInvariants(TopLevelDeclWithMembers decl) {
    return decl is IndDatatypeDecl or ClassDecl or TraitDecl;
  }

  /// <summary>
  /// The parents of the type: "raw D" (or "raw C?"), and the parents of the declaration, as the types that their
  /// names denote, "object?" included for a reference type. The declaration itself, the raw type, has as parents
  /// only the raw types of the parent traits that have invariants (see TopLevelDeclWithMembers.ParentTypes).
  /// </summary>
  public override List<Type> ParentTypes(List<Type> typeArgs, bool includeTypeBounds) {
    var result = base.ParentTypes(typeArgs, includeTypeBounds);
    result.AddRange(Owner.RawTraitsWithArgument(typeArgs));
    return result;
  }

  /// <summary>
  /// The type "raw D" for the declaration D with type arguments "typeArgs". For a class, "raw C?".
  /// </summary>
  public UserDefinedType RawType(IOrigin tok, List<Type> typeArgs) {
    return UserDefinedType.FromTopLevelDecl(tok, Owner, typeArgs);
  }

  /// <summary>
  /// The type D that the name of the declaration denotes, with type arguments "typeArgs". For a class, "C?".
  /// </summary>
  public UserDefinedType FinishedType(IOrigin tok, List<Type> typeArgs) {
    return UserDefinedType.FromTopLevelDecl(tok, this, typeArgs);
  }

  /// <summary>
  /// The type of "this" in a member of the declaration that is not raw: D, or for a class, C.
  /// </summary>
  public UserDefinedType ThisType(IOrigin tok, List<Type> typeArgs) {
    return Owner is ClassLikeDecl { NonNullTypeDecl: { } nonNullTypeDecl }
      ? UserDefinedType.FromTopLevelDecl(tok, nonNullTypeDecl, typeArgs)
      : FinishedType(tok, typeArgs);
  }

  /// <summary>
  /// The type of "this" in a raw member of the declaration: "raw D", or for a class, "raw C".
  /// </summary>
  public UserDefinedType RawThisType(IOrigin tok, List<Type> typeArgs) {
    return Owner is ClassLikeDecl { RawNonNullTypeDecl: { } rawNonNullTypeDecl }
      ? UserDefinedType.FromTopLevelDecl(tok, rawNonNullTypeDecl, typeArgs)
      : RawType(tok, typeArgs);
  }

  /// <summary>
  /// The declaration with member invariants, declared or inherited, whose raw type "type" is, or null if "type" is not
  /// raw: "type" is "raw D", or "raw C" or "raw C?", or a subset type of one of them that is not a subset type of the
  /// type that the name of the declaration denotes. Its values need not satisfy the invariants, so it is a subtype only
  /// of the raw types of the parent traits that have invariants, and not of "object" or of another trait.
  /// </summary>
  public static TopLevelDeclWithMembers? RawOwner(Type type) {
    var tower = Type.GetTowerOfSubsetTypes(type);
    return tower[0] is UserDefinedType { ResolvedClass: TopLevelDeclWithMembers { InvariantTypeDecl: { } invariantTypeDecl } owner } &&
           !tower.Exists(t => t is UserDefinedType { ResolvedClass: var decl } && decl == invariantTypeDecl)
      ? owner : null;
  }

  /// <summary>
  /// Whether "type" is a raw type (see RawOwner).
  /// </summary>
  public static bool IsRawType(Type type) {
    return RawOwner(type) != null;
  }

  /// <summary>
  /// Whether "type" is a raw reference type: "raw C" or "raw C?" for a class or reference trait C (see RawOwner).
  /// </summary>
  public static bool IsRawReferenceType(Type type) {
    return RawOwner(type) is { } owner && IsReferenceType(owner);
  }

  /// <summary>
  /// For a raw type "type" (see RawOwner), the type that the value must have before it reaches a type that is not
  /// raw: the type that the name of its declaration denotes, with the same type arguments, and for a non-null
  /// reference type, the non-null one ("C" for "raw C", "C?" for "raw C?", D for "raw D").
  /// </summary>
  public static UserDefinedType FinishedTypeOf(Type type) {
    var owner = RawOwner(type)!;
    var rawBase = (UserDefinedType)Type.GetTowerOfSubsetTypes(type)[0];
    return type.IsNonNullRefType
      ? owner.InvariantTypeDecl!.ThisType(rawBase.Origin, rawBase.TypeArgs)
      : owner.InvariantTypeDecl!.FinishedType(rawBase.Origin, rawBase.TypeArgs);
  }

  /// <summary>
  /// Issue 105: if a value of type "source" may carry a raw value (of a type "raw T") to a place where "target" does
  /// not have a raw type based on T or on a trait with invariants that T extends, such as "object", a trait without
  /// invariants, or a type that is not raw, return "source" with each such raw type replaced by the type that the name
  /// of its declaration denotes (see FinishedTypeOf): the type that the value must have before it is converted to
  /// "target". Otherwise, return null. A conversion from "raw T" to T itself is left to the membership check of the
  /// target, which checks the invariants of T. A conversion to "raw P", for a trait P with invariants that T extends,
  /// carries no obligation; one to P checks all of the invariants of T, so that a later type test of the value
  /// against T, which tests only its dynamic type, is sound.
  ///
  /// The places are the value itself and, covariantly, the elements of collections and the type arguments of other
  /// types. A raw reference in a parameter position of a function type is left to the ordinary membership check, since,
  /// in the verifier's model, "object" contains every reference that is not null.
  /// </summary>
  public static Type? FinishRawReferences(Type source, Type target) {
    if (RawOwner(source) is { } owner) {
      var targetBase = Type.GetTowerOfSubsetTypes(target)[0];
      if (targetBase is UserDefinedType { ResolvedClass: var targetDecl } && (targetDecl == owner || IsRawType(target))) {
        return null;
      }
      return FinishedTypeOf(source);
    }
    var s = source.NormalizeExpand();
    var t = target.NormalizeExpand();
    List<TypeParameter.TPVariance> variances;
    if (s is CollectionType && t is CollectionType && s.GetType() == t.GetType()) {
      variances = s.TypeArgs.ConvertAll(_ => TypeParameter.TPVariance.Co);
    } else if (s is UserDefinedType { ResolvedClass: { } decl } && t is UserDefinedType { ResolvedClass: var targetDecl } &&
               decl == targetDecl) {
      variances = s.AsArrowType is { } arrow ? arrow.Variances() : TypeParameter.Variances(decl.TypeArgs);
    } else {
      return null;
    }
    if (s.TypeArgs.Count != t.TypeArgs.Count || s.TypeArgs.Count != variances.Count) {
      return null;
    }
    var changed = false;
    var typeArgs = new List<Type>();
    for (var i = 0; i < s.TypeArgs.Count; i++) {
      var finished = variances[i] == TypeParameter.TPVariance.Co ? FinishRawReferences(s.TypeArgs[i], t.TypeArgs[i]) : null;
      changed |= finished != null;
      typeArgs.Add(finished ?? s.TypeArgs[i]);
    }
    if (!changed) {
      return null;
    }
    return s switch {
      SetType set => new SetType(set.Finite, typeArgs[0]),
      MultiSetType => new MultiSetType(typeArgs[0]),
      SeqType => new SeqType(typeArgs[0]),
      MapType map => new MapType(map.Finite, typeArgs[0], typeArgs[1]),
      ArrowType arrow => new ArrowType(arrow.Origin, (ArrowTypeDecl)arrow.ResolvedClass, typeArgs),
      UserDefinedType udt => UserDefinedType.FromTopLevelDecl(udt.Origin, udt.ResolvedClass, typeArgs),
      _ => null
    };
  }

  public override SymbolKind? Kind => Owner.Kind;

  public override string GetDescription(DafnyOptions options) {
    return Owner.GetDescription(options);
  }
}
