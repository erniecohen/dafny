//-----------------------------------------------------------------------------
//
// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
//
//-----------------------------------------------------------------------------

#nullable enable
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;

namespace Microsoft.Dafny;

/// <summary>
/// Issue 105 (--member-invariants): infers the raw members of the types that have member invariants.
///
/// A member of T is a raw member if an invariant of T applies it to "this" or to another value that is not known to
/// be a T (a value of type "raw T"), or if another raw member of T does. In a raw member, "this" has type "raw T"; in
/// every other instance member of T, it has type T.
///
/// Each instance member of such a type gets one TypeRefinementWrapper, shared by every "this" in the member. It
/// starts at T, or at "raw T" for an invariant, and a RawMemberFlow raises it to "raw T". Raising it changes the
/// types that flow from "this", so the inference is part of the fixpoint of type refinement. The fixpoint is
/// reached because a wrapper is raised at most once.
/// </summary>
public class RawMemberInference {
  private readonly Dictionary<MemberDecl, TypeRefinementWrapper> thisTypes = new();

  /// <summary>
  /// If "context" is (or belongs to) an instance member of a type with member invariants, return that member.
  /// A prefix predicate or prefix lemma belongs to its extreme predicate or lemma, and the by-method part of a
  /// function belongs to the function.
  /// </summary>
  public static MemberDecl? Member(IASTVisitorContext? context) {
    MemberDecl? member = context switch {
      PrefixPredicate prefixPredicate => prefixPredicate.ExtremePred,
      PrefixLemma prefixLemma => prefixLemma.ExtremeLemma,
      MethodOrConstructor { FunctionFromWhichThisIsByMethodDecl: { } function } => function,
      MemberDecl memberDecl => memberDecl,
      _ => null
    };
    return HasRawCounterpart(member) ? member : null;
  }

  /// <summary>
  /// Whether "member" is an instance member of a type with member invariants, so that it is applied to a receiver
  /// that may or may not be raw.
  /// </summary>
  public static bool HasRawCounterpart(MemberDecl? member) {
    return member is { IsStatic: false } and not SpecialField &&
           member.EnclosingClass is TopLevelDeclWithMembers { InvariantTypeDecl: not null };
  }

  /// <summary>
  /// The type of "this" in "context", or null if "context" is not an instance member of a type with member
  /// invariants (in which case the type of "this" is determined as usual).
  /// </summary>
  public TypeRefinementWrapper? ThisType(IASTVisitorContext? context) {
    var member = Member(context);
    return member == null ? null : ThisType(member);
  }

  private TypeRefinementWrapper ThisType(MemberDecl member) {
    if (!thisTypes.TryGetValue(member, out var wrapper)) {
      var cl = (TopLevelDeclWithMembers)member.EnclosingClass;
      var invariantTypeDecl = cl.InvariantTypeDecl!;
      var typeArgs = cl.TypeArgs.ConvertAll(tp => (Type)new UserDefinedType(tp));
      Type type = member is MemberInvariant
        ? invariantTypeDecl.RawType(member.Origin, typeArgs)
        : new UserDefinedType(member.Origin, cl.Name, invariantTypeDecl, typeArgs);
      wrapper = new TypeRefinementWrapper(type);
      thisTypes.Add(member, wrapper);
    }
    return wrapper;
  }

  /// <summary>
  /// Whether "member" is applied to raw receivers.  A member invariant is, by definition, even one whose body does
  /// not mention "this", so that the inference never sees its receiver.
  /// </summary>
  public static bool IsRawMember(MemberDecl member) {
    return member.IsRawMember || member is MemberInvariant;
  }

  /// <summary>
  /// Whether a value of type "type" is not known to satisfy the member invariants of "cl", that is, whether "type"
  /// is not "cl" (as the subset type that its name denotes) or a subset type of it.
  /// </summary>
  public static bool IsRaw(Type type, TopLevelDeclWithMembers cl) {
    if (TypeRefinementWrapper.NormalizesToBottom(type)) {
      return false;
    }
    type = TypeRefinementWrapper.NormalizeSansBottom(type);
    return !Type.GetTowerOfSubsetTypes(type).Exists(t => t is UserDefinedType { ResolvedClass: InvariantTypeDecl invariantTypeDecl } &&
                                                         invariantTypeDecl.Owner == cl);
  }

  /// <summary>
  /// Return a flow that makes "member" raw if it is applied to "receiver" in a raw member of the same type, provided
  /// that "receiver" is raw. Return null if there is nothing to infer.
  /// </summary>
  internal Flow? ApplicationFlow(IASTVisitorContext context, Expression receiver, MemberDecl member, IOrigin tok) {
    var contextMember = Member(context);
    if (contextMember == null || !HasRawCounterpart(member) || member.EnclosingClass != contextMember.EnclosingClass) {
      return null;
    }
    return new RawMemberFlow(ThisType(contextMember), receiver, ThisType(member), (TopLevelDeclWithMembers)member.EnclosingClass,
      tok, member.Name);
  }

  /// <summary>
  /// Record the result of the inference in the members, once type refinement has reached its fixpoint.
  /// </summary>
  public void Finish() {
    foreach (var (member, wrapper) in thisTypes) {
      member.IsRawMember = IsRaw(wrapper, (TopLevelDeclWithMembers)member.EnclosingClass);
    }
  }
}

/// <summary>
/// If the member "context" is raw and "receiver" is raw, then the member applied to "receiver" is raw.
/// Each of "contextThis" and "memberThis" is the shared type of "this" in a member.
/// </summary>
class RawMemberFlow : Flow {
  private readonly TypeRefinementWrapper contextThis;
  private readonly Expression receiver;
  private readonly TypeRefinementWrapper memberThis;
  private readonly TopLevelDeclWithMembers cl;

  public RawMemberFlow(TypeRefinementWrapper contextThis, Expression receiver, TypeRefinementWrapper memberThis,
    TopLevelDeclWithMembers cl, IOrigin tok, string memberName)
    : base(tok, $"raw member {memberName}") {
    this.contextThis = contextThis;
    this.receiver = receiver;
    this.memberThis = memberThis;
    this.cl = cl;
  }

  public override bool Update(FlowContext context) {
    if (RawMemberInference.IsRaw(memberThis, cl) || !RawMemberInference.IsRaw(contextThis, cl) ||
        !RawMemberInference.IsRaw(receiver.UnnormalizedType, cl)) {
      return false;
    }
    var thisType = (UserDefinedType)memberThis.T;
    Contract.Assert(thisType.ResolvedClass is InvariantTypeDecl);
    memberThis.T = cl.InvariantTypeDecl!.RawType(thisType.Origin, thisType.TypeArgs);
    if (context.DebugPrint) {
      context.Reporter.Options.OutputWriter.Debug($"refining {TokDescription()} to {memberThis.T}");
    }
    return true;
  }

  public override void DebugPrint(TextWriter output) {
    var receiverType = TypeRefinementWrapper.ToStringShowingWrapper(receiver.UnnormalizedType);
    var bound = PreTypeConstraints.Pad($"%{memberThis.UniqueId} :> raw if %{contextThis.UniqueId}, {receiverType}", 27);
    var value = PreTypeConstraints.Pad(TypeRefinementWrapper.ToStringAsBottom(memberThis), 20);
    output.WriteLine($"    {bound}  {value}    {TokDescription()}");
  }
}
