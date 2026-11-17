// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) of non-generic datatypes without
// parents: the examples of sections 6.1 and 6.3 of docs/dev/issue105-member-invariants-plan.md, and
// the vacuity controls V1, V4 and V8 of its section 6.4.  A value of the type satisfies its
// invariants, a raw value is converted to it only where they are proved, and a witness, and every
// value the verifier takes as a default, satisfies them.

module Intervals {
  datatype Interval = Interval(lo: int, hi: int) {
    invariant lo <= hi
    witness Interval(0, 0)

    function Width(): nat { hi - lo }
    function Shift(d: int): Interval { this.(lo := lo + d).(hi := hi + d) }
    ghost predicate Tightest()
    { forall j: Interval :: j.lo <= lo && hi <= j.hi ==> j == this }
  }

  method Use(i: Interval) {
    assert i.lo <= i.hi;
    var j: Interval := Interval(i.lo, i.hi + 1);
    var r := Interval(i.hi, i.lo);  // raw: no obligation
    assert r.lo >= r.hi;
  }

  // V1: the equivalence for a datatype does not make the invariant false.
  method V1(i: Interval) { assert false; }  // error

  // V8: a raw value is not taken for a finished one.
  method V8(r: raw Interval) { var i: Interval := r; }  // error: the invariant might not hold
}

module Trees {
  datatype Tree = Leaf | Node(left: Tree, key: int, right: Tree) {
    predicate Ordered() {
      Node? ==> (forall k <- left.Keys() :: k < key) && (forall k <- right.Keys() :: key < k)
    }
    invariant Ordered()
    function Keys(): set<int> { if Leaf? then {} else left.Keys() + {key} + right.Keys() }
  }

  function Rebuild(t: Tree, k: int): raw Tree

  lemma {:axiom} InsertOrdered(r: raw Tree, t: Tree, k: int)
    requires r == Rebuild(t, k)
    ensures r.Ordered()

  method Insert(t: Tree, k: int) returns (u: Tree) {
    var r := Rebuild(t, k);
    InsertOrdered(r, t, k);
    u := r;
  }

  datatype Sorted = Nil | Cons(head: int, tail: Sorted) {
    invariant Cons? && tail.Cons? ==> head <= tail.head
    witness Nil

    function Push(v: int): Sorted
      requires Nil? || v <= head
    { Cons(v, this) }
  }
}

module Roses {
  datatype Rose = Node(size: nat, kids: seq<Rose>) {
    invariant forall k <- kids :: k.size < size
    witness Node(0, [])
  }

  // V4: a recursive invariant does not make the type empty.
  method V4() { var t: Rose := Node(1, [Node(0, [])]); assert false; }  // error
}

// Section 6.3: the obligations of witnesses, members and defaults.
module Obligations {
  datatype D = D(x: nat, y: nat) {
    invariant x == y
    witness D(0, 1)  // error: the witness does not satisfy the invariant
    function Inc(): D { this.(x := x + 1) }  // error, twice: the result does not satisfy either invariant
    function Gap(): nat { x - y }  // error: 'this' is raw in the second invariant, so x - y may be negative
    invariant Gap() == 0
  }

  // No witness, and Mk(0) is no value of Empty: Empty is possibly empty.
  datatype Empty = Mk(n: nat) { invariant n < 0 }

  method Consume(e: Empty) {}

  method M() {
    var e: Empty;
    Consume(e);  // error: 'e' is used before it is assigned
  }

  datatype Ghostly = Ghostly(ghost p: int) {
    invariant p > 3
    ghost witness Ghostly(4)
  }

  method G00<T(00)>() {}

  method UseGhostly() {
    G00<Ghostly>();
  }
}
