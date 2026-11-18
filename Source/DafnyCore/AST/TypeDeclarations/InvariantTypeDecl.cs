#nullable enable
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Diagnostics.CodeAnalysis;
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
    : this(owner, TypeParametersOf(owner.TypeArgs)) {
  }

  /// <summary>
  /// Unresolved clones of the owner's type parameters, with the owner's variance. A value of D&lt;A&gt; satisfies the
  /// invariants as instantiated with A, which implies them as instantiated with a larger type B only where they are
  /// monotone in the type parameter: an invariant may quantify over it. So a covariant (or contravariant) type
  /// parameter requires the invariants to be monotone (or antitone) in it, which the resolver checks
  /// (TypeParameterPolarity.CheckInvariantVariance); a conversion between instances that are not subtypes is checked
  /// as a conversion to any subset type is. "raw D" has the owner's variance too.
  /// </summary>
  private static List<TypeParameter> TypeParametersOf(List<TypeParameter> typeParameters) {
    var cloner = new Cloner();
    return typeParameters.ConvertAll(tp => new TypeParameter(tp.Origin, tp.NameNode, tp.VarianceSyntax,
      tp.Characteristics, tp.TypeBounds.ConvertAll(cloner.CloneType), tp.Attributes));
  }

  private InvariantTypeDecl(TopLevelDeclWithMembers owner, List<TypeParameter> tps)
    : this(owner, tps, new BoundVar(owner.Origin, "v", RawType(owner, tps))) {
  }

  private InvariantTypeDecl(TopLevelDeclWithMembers owner, List<TypeParameter> tps, BoundVar var)
    : this(owner, tps, var, owner.Members.OfType<MemberWitness>().FirstOrDefault(), (owner as NewtypeDecl)?.TakeHeaderClauses()) {
  }

  private InvariantTypeDecl(TopLevelDeclWithMembers owner, List<TypeParameter> tps, BoundVar var, MemberWitness? witness,
    NewtypeDecl.HeaderClauses? header)
    : this(owner, tps, var, witness, header, HeaderConjunctOf(owner, var, header)) {
  }

  private InvariantTypeDecl(TopLevelDeclWithMembers owner, List<TypeParameter> tps, BoundVar var, MemberWitness? witness,
    NewtypeDecl.HeaderClauses? header, Expression? headerConjunct)
    : base(owner.Origin, owner.NameNode,
      new TypeParameterCharacteristics(TypeParameter.EqualitySupportValue.Unspecified, AutoInit(owner, witness, header), false),
      tps, owner.EnclosingModuleDefinition, var, OwnConstraintOf(owner, var, headerConjunct), WitnessKindOf(owner, witness, header),
      witness == null && header != null ? header.Witness : WitnessReference(owner, tps, witness), null) {
    Owner = owner;
    HeaderConjunct = headerConjunct;
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

  private static Type.AutoInitInfo AutoInit(TopLevelDeclWithMembers owner, MemberWitness? witness, NewtypeDecl.HeaderClauses? header) {
    return IsReferenceType(owner) ? Type.AutoInitInfo.CompilableValue
      : owner is AbstractTypeDecl abstractTypeDecl ? PromisedAutoInit(abstractTypeDecl)
      : witness == null && header != null ? header.WitnessKind switch {
        WKind.OptOut => Type.AutoInitInfo.MaybeEmpty,
        WKind.Ghost => Type.AutoInitInfo.Nonempty,
        _ => Type.AutoInitInfo.CompilableValue
      }
      : witness == null ? Type.AutoInitInfo.MaybeEmpty
      : witness.IsGhost ? Type.AutoInitInfo.Nonempty : Type.AutoInitInfo.CompilableValue;
  }

  /// <summary>
  /// The characteristics that the header of an abstract type with invariants states, "(0)" or "(00)", are promises
  /// about the type with its invariants, which every refinement keeps by supplying a witness (section 4.3 of the
  /// plan). The refinement's characteristics are checked where it refines the abstract type. Only an abstract module
  /// may make such a promise (ModuleResolver), since only it is refined before any concrete use.
  /// </summary>
  public static Type.AutoInitInfo PromisedAutoInit(AbstractTypeDecl owner) {
    return owner.Characteristics.HasCompiledValue ? Type.AutoInitInfo.CompilableValue
      : owner.Characteristics.IsNonempty ? Type.AutoInitInfo.Nonempty : Type.AutoInitInfo.MaybeEmpty;
  }

  /// <summary>
  /// A reference type has the witness null, which satisfies the constraint, as the base type's default value does.
  /// An abstract type has no witness of its own: what its characteristics promise, a refinement supplies (see
  /// PromisedAutoInit), so it opts out of the witness check, and where it is compiled, its default value is that of
  /// "raw T". A newtype in the old syntax without a witness member has the witness its header gives, with the old
  /// meaning, the default included: with no witness clause, the base type's zero must satisfy the constraint.
  /// </summary>
  private static WKind WitnessKindOf(TopLevelDeclWithMembers owner, MemberWitness? witness, NewtypeDecl.HeaderClauses? header) {
    return IsReferenceType(owner) ? WKind.CompiledZero
      : witness == null && header != null ? header.WitnessKind
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
  private static Expression OwnConstraintOf(TopLevelDeclWithMembers owner, BoundVar var, Expression? headerConjunct) {
    return Guarded(owner, var, OwnInvariants(owner, var, headerConjunct) ?? new LiteralExpr(owner.Origin, true));
  }

  /// <summary>
  /// The constraint of the header of a newtype in the old syntax, "x: B | P(x)", as the conjunct "var x: B := v as B;
  /// P(x)" (see NewtypeDecl.HeaderClauses), or null. It reuses the expression of the header, which the newtype has
  /// given up, so that it is resolved once. It is resolved here, where the type parameters in scope are those of this
  /// type, so "x" is a variable of its own, of a copy of the type B, which the newtype has resolved as its base type,
  /// with its own type parameters. Where B is not written, the newtype infers it from P, as without invariants, so
  /// "x" is the variable of the header, and P is resolved with the newtype (see PreTypeResolver.HeaderToInfer).
  /// </summary>
  private static Expression? HeaderConjunctOf(TopLevelDeclWithMembers owner, BoundVar var, NewtypeDecl.HeaderClauses? header) {
    if (header?.Var is not { } headerVar) {
      return null;
    }
    var tok = header.Constraint.Origin;
    var letVar = headerVar.Type is TypeProxy ? headerVar : new Cloner().CloneBoundVar(headerVar, false);
    return new LetExpr(tok, [new CasePattern<BoundVar>(letVar.Origin, letVar)],
      [new ConversionExpr(tok, new IdentifierExpr(tok, var), letVar.Type)], header.Constraint, true);
  }

  /// <summary>
  /// The constraint of the header of a newtype in the old syntax, as a conjunct of the invariants (see
  /// HeaderConjunctOf), or null.
  /// </summary>
  public readonly Expression? HeaderConjunct;

  /// <summary>
  /// The conjunct of the header, if any, and then the invariants that "owner" declares, applied to "var". The
  /// constraint of the header may have the type of a newtype based on bool, as without invariants, so the conjunct is
  /// its conversion to bool, which the verifier translates as the constraint itself (see IsHeaderConjunct).
  /// </summary>
  private static Expression? OwnInvariants(TopLevelDeclWithMembers owner, BoundVar var, Expression? headerConjunct) {
    var invariants = Invariants(owner, var);
    if (headerConjunct == null) {
      return invariants;
    }
    var header = new ConversionExpr(headerConjunct.Origin, headerConjunct, Type.Bool);
    return invariants == null ? header : Conjoin(header, invariants);
  }

  /// <summary>
  /// Whether "conjunct", one of the conjuncts of the constraint of an InvariantTypeDecl, is that of the header (see
  /// OwnInvariants); if so, "headerConstraint" is the constraint of the header, as HeaderConjunct has it.
  /// </summary>
  public static bool IsHeaderConjunct(Expression conjunct, [NotNullWhen(true)] out Expression? headerConstraint) {
    headerConstraint = conjunct.Resolved is ConversionExpr { E.Resolved: LetExpr let } ? let : null;
    return headerConstraint != null;
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
    var own = OwnInvariants(Owner, Var, HeaderConjunct);
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
    if (invariantTypeDecl.HeaderConjunct is LetExpr { LHSs: [{ Var: { } headerVar }], Body: var headerConstraint }) {
      // The constraint of the header comes before the invariants, as "P(this as B)", with the type parameters of the
      // owner for those of this type (see HeaderConjunctOf)
      var subst = TypeParameter.SubstitutionMap(invariantTypeDecl.TypeArgs, typeArgs);
      var baseType = headerVar.Type.Subst(subst);
      var baseValue = new ConversionExpr(tok, thisExpr, baseType) { Type = baseType };
      yield return new Substituter(null, new Dictionary<IVariable, Expression> { { headerVar, baseValue } }, subst)
        .Substitute(headerConstraint);
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
  /// class, a trait, an abstract type or a newtype that declares an invariant (in itself or, by refinement, in a declaration it refines). One that
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
    return decl is IndDatatypeDecl or ClassDecl or TraitDecl or AbstractTypeDecl or NewtypeDecl;
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
  /// A newtype N with member invariants such that "type" mentions "raw N", or null if there is none. A compiled value
  /// of N satisfies the invariants, so that its native representation can be read from them (see NativeTypeAnalysis),
  /// and "raw N" is ghost.
  /// </summary>
  public static NewtypeDecl? GhostRawNewtype(Type type) {
    if (RawOwner(type) is NewtypeDecl newtypeDecl) {
      return newtypeDecl;
    }
    foreach (var typeArg in type.NormalizeExpandKeepConstraints().TypeArgs) {
      if (GhostRawNewtype(typeArg) is { } inner) {
        return inner;
      }
    }
    return null;
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
