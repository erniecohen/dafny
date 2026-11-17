#nullable enable
using System.Collections.Generic;

namespace Microsoft.Dafny;

/// <summary>
/// Issue 105 (--member-invariants): for a class C with member invariants, the type "raw C", whose values are the
/// non-null references whose dynamic type is C, whether or not they satisfy the invariants. Its base is "raw C?",
/// the class declaration itself, and it is a subtype of that and of the raw non-null types of the parent traits that
/// have invariants ("raw P"), and of nothing else: not of "object", and not of another parent trait. C is a subtype of
/// "raw C" (see NonNullTypeDecl.ParentTypes). A reference trait with member invariants has one too.
///
/// Like the derived type "C?" (an InvariantTypeDecl), it is not registered under a name of its own. The resolver
/// maps "raw C", written as a type, to it.
/// </summary>
public class RawNonNullTypeDecl : NonNullTypeDecl {
  public RawNonNullTypeDecl(ClassLikeDecl cl)
    : base(cl, true) {
    // "raw C" is visible where its base, the class declaration "raw C?", is: where the class is revealed
    InheritVisibility(cl, false);
    // As for an InvariantTypeDecl, make again the synonym that stands for this type where it is not revealed, so
    // that it has that visibility too.
    this.NewSelfSynonym();
  }

  public override List<Type> ParentTypes(List<Type> typeArgs, bool includeTypeBounds) {
    var result = new List<Type> { RhsWithArgument(typeArgs) };
    foreach (var rawParent in Class.RawParentTypesWithInvariants(typeArgs)) {
      result.Add(UserDefinedType.CreateNonNullTypeIfReferenceType((UserDefinedType)rawParent));
    }
    return result;
  }
}
