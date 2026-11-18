using System.Collections.Generic;
using System.Diagnostics.Contracts;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.Dafny;

public class NonNullTypeDecl : SubsetTypeDecl {
  public override string WhatKind => "non-null type";
  public ClassLikeDecl Class;

  /// <summary>
  /// The public constructor is NonNullTypeDecl(ClassDecl cl). The rest is pretty crazy: There are stages of "this"-constructor calls
  /// in order to build values that depend on previously computed parameters.
  /// </summary>
  public NonNullTypeDecl(ClassLikeDecl cl)
    : this(cl, false) {
    Contract.Requires(cl != null);
  }

  /// <summary>
  /// The non-null type of "cl", whose base is "C?" or, if "raw" (issue 105), "raw C?".
  /// </summary>
  protected NonNullTypeDecl(ClassLikeDecl cl, bool raw)
    : this(cl, TypeParameter.CloneTypeParameters(cl.TypeArgs), raw) {
    Contract.Requires(cl != null);
  }

  private NonNullTypeDecl(ClassLikeDecl cl, List<TypeParameter> tps, bool raw)
    : this(cl, tps,
      new BoundVar(cl.Origin, "c", new UserDefinedType(cl.Origin, cl.Name + "?", tps.Count == 0 ? null : tps.ConvertAll(tp => (Type)new UserDefinedType(tp))) {
        IsRawSyntax = raw
      })) {
    Contract.Requires(cl != null);
    Contract.Requires(tps != null);
  }

  private NonNullTypeDecl(ClassLikeDecl cl, List<TypeParameter> tps, BoundVar var)
    : base(cl.Origin, cl.NameNode, new TypeParameterCharacteristics(), tps, cl.EnclosingModuleDefinition, var,
      new BinaryExpr(cl.Origin, BinaryExpr.Opcode.Neq, new IdentifierExpr(cl.Origin, var), new LiteralExpr(cl.Origin)),
      SubsetTypeDecl.WKind.Special, null, SystemModuleManager.AxiomAttribute()) {
    Contract.Requires(cl != null);
    Contract.Requires(tps != null);
    Contract.Requires(var != null);
    Class = cl;
  }

  public override List<Type> ParentTypes(List<Type> typeArgs, bool includeTypeBounds) {
    var result = new List<Type>(base.ParentTypes(typeArgs, includeTypeBounds));

    // The parents of a class with member invariants are the parents of the type "C?" that its name denotes, not of
    // the raw type "raw C?" that is the class declaration itself (issue 105).
    var classParentTypes = Class.InvariantTypeDecl != null ? Class.RawTraitsWithArgument(typeArgs) : Class.ParentTypes(typeArgs, includeTypeBounds);
    foreach (var rhsParentType in classParentTypes) {
      var rhsParentUdt = (UserDefinedType)rhsParentType; // all parent types of .Class are expected to be possibly-null class types
      // Issue 105: or the derived "P?" of a trait P with member invariants, or the derived type G of a general trait
      Contract.Assert(rhsParentUdt.ResolvedClass is TraitDecl or InvariantTypeDecl { Owner: TraitDecl });
      result.Add(UserDefinedType.CreateNonNullTypeIfReferenceType(rhsParentUdt));
    }
    if (Class is ClassLikeDecl { RawNonNullTypeDecl: { } rawNonNullTypeDecl }) {
      // Issue 105: C is a subtype of raw C
      result.Add(UserDefinedType.FromTopLevelDecl(Origin, rawNonNullTypeDecl, typeArgs));
    }

    return result;
  }

  public override SymbolKind? Kind => Class.Kind;

  public override string GetDescription(DafnyOptions options) {
    return Class.GetDescription(options);
  }
}