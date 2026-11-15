// Well-founded orders for decreases clauses (https://github.com/erniecohen/dafny/issues/245).
//
// With --decreases-orders, a component "e by O" of a decreases clause, where O: Wfo<T> and e: T,
// is compared by O.gt: a call or loop iteration must make O.gt(old e, e) true, or leave e unchanged.
// Every implementation of Wfo verifies the postcondition of aha, so the gt of every value of type
// Wfo<T> is well-founded.
//
// This module is a library of its own, DafnyStandardLibraries-orders.doo, loaded with the standard
// library under --decreases-orders, --type-system-refresh and --general-traits=datatype (or full),
// since the datatype WfoOf implements the trait Wfo.  An order is usually made with Of:
//
//   ghost function UpOrder(): Wfo<nat> { UpWellFounded(); Of(Up) }   // UpWellFounded proves wellFounded(Up)
module Std.Termination.Orders {
  // An infinite sequence of values of T
  type {:termination_closed} Chain<!T(!new)> = nat -> T

  trait Wfo<!T(!new)> {
    // gt(x, y) says that x is greater than y
    ghost predicate gt(x: T, y: T)
    // Every chain stops descending somewhere: aha(c) is such a place
    ghost function aha(c: Chain<T>): (r: nat) ensures !gt(c(r), c(r + 1))
    ghost predicate isZ(t: T) { forall t1 :: !gt(t, t1) }
    ghost predicate ge(t0: T, t1: T) { gt(t0, t1) || t0 == t1 }
    ghost predicate le(t0: T, t1: T) { ge(t1, t0) }
  }

  ghost predicate stops<T(!new)>(gt: (T, T) -> bool, c: Chain<T>) { exists n: nat :: !gt(c(n), c(n + 1)) }
  ghost predicate wellFounded<T(!new)>(gt: (T, T) -> bool) { forall c: Chain<T> :: stops(gt, c) }

  lemma wfoWellFounded<T(!new)>(w: Wfo<T>) ensures wellFounded(w.gt) {
    forall c: Chain<T> ensures stops(w.gt, c) { var n := w.aha(c); }
  }

  // gt is r if r is well-founded, and empty otherwise
  datatype {:termination_closed} WfoOf<!T(!new)> extends Wfo<T> = WfoOf(r: (T, T) -> bool) {
    ghost predicate gt(x: T, y: T) { wellFounded(r) && r(x, y) }
    ghost function aha(c: Chain<T>): (n: nat) ensures !gt(c(n), c(n + 1)) {
      if wellFounded(r) then assert stops(r, c); var n: nat :| !r(c(n), c(n + 1)); n else 0
    }
  }

  // The order whose gt is r.  Its postcondition gives gt directly, so a client of the order does not
  // depend on how a call of gt through the trait reaches WfoOf.gt.
  ghost function Of<T(!new)>(r: (T, T) -> bool): (o: Wfo<T>)
    requires wellFounded(r)
    ensures forall x: T, y: T :: o.gt(x, y) == r(x, y)
  {
    var w := WfoOf(r);
    forall x: T, y: T ensures (w as Wfo<T>).gt(x, y) == r(x, y) {
      assert w.gt(x, y) == r(x, y);
    }
    w
  }
}
