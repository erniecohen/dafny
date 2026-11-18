// The invariant extension condition follows calls across a recursive module group. The helper's
// unbounded quantifier over the owner's values must be rejected, as it would be in one module.
// RUN: %exits-with 2 %resolve --member-invariants --recursive-modules "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module Owner {
  import Helper
  datatype Liar = Liar(n: int) {
    invariant Helper.No()
  }
}
module Helper {
  import Owner
  ghost predicate No() { !(exists x: Owner.Liar :: x.n == 0) }
}
