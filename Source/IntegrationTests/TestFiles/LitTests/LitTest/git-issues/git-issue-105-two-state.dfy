// Member invariants (https://github.com/erniecohen/dafny/issues/105): two-state features are rejected
// in an invariant, as in any other single-state predicate.
// RUN: %exits-with 2 %resolve --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

class D {
  var f: int
  const k: int
  twostate function Changed(): bool reads this { old(f) != f }
  invariant old(k) == k
  invariant unchanged(this)
  invariant fresh(this)
  invariant allocated(this)
  invariant Changed()
}
