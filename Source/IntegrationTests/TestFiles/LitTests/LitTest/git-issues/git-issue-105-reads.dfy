// Member invariants (https://github.com/erniecohen/dafny/issues/105): an invariant depends on no
// mutable state.  It reads no mutable field and no array element, uses no function with a reads
// clause, and applies no function value that may read the heap.  It may follow const fields, read
// the lengths of arrays, and call functions without a reads clause.
// RUN: %exits-with 2 %resolve --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

class C {
  var f: int
  const k: int
  const a: array<int>
  const m: array2<int>
  const next: C?
  const total: int -> int
  const partial: int --> int
  const reading: int ~> int
  function Reads(): int reads this { f }
  function Pure(): int { k }
  static function Twice(x: int): int { 2 * x }

  // allowed
  invariant k > 0 && a.Length > 0 && m.Length0 > 0 && Pure() > 0 && Twice(k) > 0
  invariant total(0) > 0 && (partial.requires(0) ==> partial(0) > 0)
  invariant next != null ==> next.k > 0 && next.a.Length > 0

  // not allowed
  invariant f > 0
  invariant next != null ==> next.f > 0
  invariant a[0] > 0
  invariant a[..] == []
  invariant m[0, 0] > 0
  invariant Reads() > 0
  invariant (var g := Reads; true)
  invariant reading(0) > 0
  invariant forall i :: 0 <= i < a.Length ==> a[i] > 0
}
