#nullable enable
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Dafny.Auditor;

namespace Microsoft.Dafny;

public abstract class MemberDecl : Declaration, ISymbol {
  [FilledInDuringResolution] public OperatorDescriptor? Operator { get; set; }
  public abstract string WhatKind { get; }
  public string WhatKindAndName => $"{WhatKind} '{Name}'";
  public virtual string WhatKindMentionGhost => (IsGhost ? "ghost " : "") + WhatKind;
  public abstract bool HasStaticKeyword { get; }
  public virtual bool IsStatic => HasStaticKeyword || EnclosingClass is DefaultClassDecl;

  public virtual bool IsOpaque => false;

  protected bool isGhost;
  public bool IsGhost { get { return isGhost; } }

  public string ModifiersAsString() {
    string result = "";
    if (IsGhost) {
      result += "ghost ";
    }
    if (IsStatic) {
      result += "static ";
    }
    if (IsOpaque) {
      result += "opaque ";
    }
    return result;
  }

  /// <summary>
  /// The term "instance independent" can be confusing. It means that the constant does not get its value in
  /// a constructor. (But the RHS of the const's declaration may mention "this".)
  /// </summary>
  public bool IsInstanceIndependentConstant => this is ConstantField cf && cf.Rhs != null;

  [FilledInDuringResolution] public TopLevelDecl EnclosingClass = null!;
  [FilledInDuringResolution] public MemberDecl? RefinementBase;  // filled in during the pre-resolution refinement transformation; null if the member is new here
  [FilledInDuringResolution] public MemberDecl? OverriddenMember;  // non-null if the member overrides a member in a parent trait
  public virtual bool IsOverrideThatAddsBody => OverriddenMember != null;

  /// <summary>
  /// The member was declared in a trait under --inherited-trait-axioms.  The parser sets this, on the
  /// members of a trait only, from the options of the file that declares the member, and cloning and
  /// refinement keep it, so a member's status as a trait axiom is fixed where it is declared: a library
  /// built without the option keeps its bodiless {:axiom} trait members as members to implement, also
  /// for a client that uses the option (https://github.com/erniecohen/dafny/issues/89).
  /// </summary>
  public bool DeclaredUnderInheritedTraitAxioms;

  /// <summary>
  /// A bodiless member that a trait declares with {:axiom} under --inherited-trait-axioms, whether or
  /// not it is of a kind that can be a trait axiom.
  /// </summary>
  public bool IsBodilessAxiomOfTrait =>
    DeclaredUnderInheritedTraitAxioms && !IsStatic && HasAxiomAttribute &&
    this is (Function { Body: null } or MethodOrConstructor { Body: null });

  /// <summary>
  /// A trait axiom: a class, datatype or newtype that inherits it need not implement it, and its
  /// specification is a declared axiom of the trait.  It is a bodiless ghost trait member with {:axiom},
  /// not extreme, declared under --inherited-trait-axioms.
  /// </summary>
  public bool IsInheritedTraitAxiom =>
    IsBodilessAxiomOfTrait && IsGhost &&
    this is not (ExtremePredicate or ExtremeLemma or PrefixPredicate or PrefixLemma);

  /// <summary>
  /// Returns "true" if "this" is a (possibly transitive) override of "possiblyOverriddenMember".
  /// </summary>
  public bool Overrides(MemberDecl possiblyOverriddenMember) {
    Contract.Requires(possiblyOverriddenMember != null);
    for (var th = this; th != null; th = th.OverriddenMember) {
      if (th == possiblyOverriddenMember) {
        return true;
      }
    }
    return false;
  }

  protected MemberDecl(Cloner cloner, MemberDecl original) : base(cloner, original) {
    this.EnclosingClass = original.EnclosingClass;
    this.isGhost = original.isGhost;
  }

  [SyntaxConstructor]
  protected MemberDecl(IOrigin origin, Name nameNode, bool isGhost, Attributes? attributes)
    : base(origin, nameNode, attributes) {
    Contract.Requires(origin != null);
    Contract.Requires(nameNode != null);
    this.isGhost = isGhost;
  }

  /// <summary>
  /// Returns className+"."+memberName.  Available only after resolution.
  /// </summary>
  public virtual string FullDafnyName {
    get {
      Contract.Requires(EnclosingClass != null);
      Contract.Ensures(Contract.Result<string>() != null);
      string n = EnclosingClass!.FullDafnyName;
      return (n.Length == 0 ? n : (n + ".")) + Name;
    }
  }
  public virtual string FullName {
    get {
      Contract.Requires(EnclosingClass != null);
      Contract.Ensures(Contract.Result<string>() != null);

      return EnclosingClass!.FullName + "." + Name;
    }
  }

  public override string SanitizedName =>
    (Name == EnclosingClass.Name ? "_" : "") + base.SanitizedName;

  public override string GetCompileName(DafnyOptions options) =>
    (Name == EnclosingClass.Name ? "_" : "") + base.GetCompileName(options);

  public virtual string FullSanitizedName {
    get {
      Contract.Ensures(Contract.Result<string>() != null);

      if (Name == "requires") {
        return BoogieGenerator.Requires(((ArrowTypeDecl)EnclosingClass!).Arity);
      } else if (Name == "reads") {
        return BoogieGenerator.Reads(((ArrowTypeDecl)EnclosingClass!).Arity);
      } else {
        return EnclosingClass!.FullSanitizedName + "." + SanitizedName;
      }
    }
  }

  public virtual IEnumerable<Expression> SubExpressions => [];

  public override IEnumerable<Assumption> Assumptions(Declaration decl) {
    foreach (var a in base.Assumptions(this)) {
      yield return a;
    }
    if (Attributes.Find(this.Attributes, "only") != null) {
      yield return new Assumption(decl, Origin, AssumptionDescription.MemberOnly);
    }
    if (IsInheritedTraitAxiom) {
      yield return new Assumption(this, Origin, AssumptionDescription.TraitAxiom(this is Function, EnclosingClass.Name));
    }
  }

  public void RecursiveCallParameters(IOrigin tok, List<TypeParameter> typeParams, List<Formal> ins,
    Expression receiverSubst, Dictionary<IVariable, Expression> substMap,
    out Expression receiver, out List<Expression> arguments) {
    Contract.Requires(tok != null);
    Contract.Requires(this != null);
    Contract.Requires(EnclosingClass is TopLevelDeclWithMembers);
    Contract.Requires(typeParams != null);
    // receiverSubst is allowed to be null
    Contract.Ensures(Contract.ValueAtReturn(out receiver) != null);
    Contract.Ensures(Contract.ValueAtReturn(out arguments) != null);

    if (IsStatic) {
      receiver = new StaticReceiverExpr(tok, (TopLevelDeclWithMembers)EnclosingClass, true); // this also resolves it
    } else if (receiverSubst != null) {
      receiver = receiverSubst;
    } else {
      receiver = new ImplicitThisExpr(tok);
      receiver.Type = ModuleResolver.GetReceiverType(tok, this);  // resolve here
    }

    arguments = [];
    foreach (var inFormal in ins) {
      if (substMap.TryGetValue(inFormal, out var inE)) {
        arguments.Add(inE);
      } else {
        var ie = new IdentifierExpr(inFormal.Origin, inFormal.Name);
        ie.Var = inFormal;  // resolve here
        ie.Type = inFormal.Type;  // resolve here
        arguments.Add(ie);
      }
    }
  }
}