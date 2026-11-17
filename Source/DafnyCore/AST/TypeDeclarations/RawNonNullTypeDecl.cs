#nullable enable
using System.Collections.Generic;

namespace Microsoft.Dafny;

/// <summary>
/// Issue 105 (--member-invariants): for a class C with member invariants, the type "raw C", whose values are the
/// non-null references whose dynamic type is C, whether or not they satisfy the invariants. Its base is "raw C?",
/// the class declaration itself, and it is a subtype of that and of nothing else: not of "object", and not of a
/// parent trait. C is a subtype of "raw C" (see NonNullTypeDecl.ParentTypes).
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
    return [RhsWithArgument(typeArgs)];
  }
}
