#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// A member invariant, "invariant e", in a datatype, class, trait or newtype (issue 105, under
/// --member-invariants). It is represented as an instance ghost predicate with no parameters, no reads
/// clause and the reserved name "invariant#k", where k numbers the invariants of the declaration in
/// textual order. The name cannot be written in source, so the predicate cannot be called directly;
/// representing it as a predicate reuses resolution, well-formedness checking, the call graph and the
/// generation of a Boogie function.
/// </summary>
public class MemberInvariant : Predicate {
  public const string NamePrefix = "invariant#";
  public override string WhatKind => "invariant";

  public static string NextName(IEnumerable<MemberDecl> members) {
    return NamePrefix + members.OfType<MemberInvariant>().Count();
  }

  public static bool IsReservedName(string name) {
    return name.StartsWith(NamePrefix);
  }

  /// <summary>
  /// The number k of the reserved name "invariant#k". A refining declaration numbers its new invariants after
  /// those it inherits, but may list its members in another order, so the numbers, not the order of the
  /// members, give the order of the invariants.
  /// </summary>
  public int Number => int.Parse(Name[NamePrefix.Length..]);

  public MemberInvariant(IOrigin origin, IOrigin keywordToken, string name, Expression body, Attributes? attributes)
    : base(origin, new Name(keywordToken, name), false, true, false,
      [], [], null, [], new Specification<FrameExpression>([], null), [],
      new Specification<Expression>([], null), body, BodyOriginKind.OriginalOrInherited,
      null, null, attributes, null) {
  }
}

/// <summary>
/// A witness member, "witness e" or "ghost witness e", in a type with member invariants (issue 105,
/// under --member-invariants). It is represented as a static const with an inferred type and the
/// reserved name "witness#k".
/// </summary>
public class MemberWitness : ConstantField {
  public const string NamePrefix = "witness#";
  public override string WhatKind => "witness";

  public static string NextName(IEnumerable<MemberDecl> members) {
    return NamePrefix + members.OfType<MemberWitness>().Count();
  }

  public MemberWitness(Cloner cloner, MemberWitness original) : base(cloner, original) {
  }

  public MemberWitness(IOrigin origin, IOrigin keywordToken, string name, bool isGhost, Expression rhs, Attributes? attributes)
    : base(origin, new Name(keywordToken, name), rhs, true, isGhost, false, null, attributes) {
  }
}
