// Vacuity control for github-issue-211-rank.dfy (https://github.com/erniecohen/dafny/issues/211, milestone M4a):
// the same program with "assert false" in every body.  Each must fail: the bounds give no contradictory facts.

module Tr {
  datatype {:termination_closed} List = Nil | Cons(head: nat, tail: List)

  function {:termination_closed} Size(xs: List): nat {
    assert false;
    match xs
    case Nil => 0
    case Cons(_, tail) => 1 + Size(tail)
  }

  type {:termination_closed} Small = x: nat | x < 100

  trait {:termination false} {:termination_closed} T extends object {
    var fuel: nat
    function F(xs: List): nat
      reads *
      decreases {:global} Size(xs)
  }
}

module Closed {
  import Tr

  const Weight: nat := 2

  function Fuel(t: Tr.T): nat reads t { assert false; t.fuel }

  function ViaDatatype(t: Tr.T, xs: Tr.List): nat
    reads *
    decreases {:global} Tr.Size(xs) + 1
  {
    assert false; t.F(xs)
  }

  method ViaHeap(t: Tr.T, xs: Tr.List) returns (r: nat)
    decreases {:global} Fuel(t) + Tr.Size(xs) + 1
  {
    assert false;
    r := t.F(xs);
  }

  function ViaLambda(t: Tr.T, xs: Tr.List): nat
    reads *
    decreases {:global} ((ys: Tr.List) => Tr.Size(ys) + 1)(xs)
  {
    assert false; t.F(xs)
  }

  function ViaSubsetType(t: Tr.T, xs: Tr.List, s: Tr.Small): nat
    reads *
    decreases {:global} Tr.Size(xs) + (if s is Tr.Small then 1 else 2)
  {
    assert false; t.F(xs)
  }

  function ViaConstant(t: Tr.T, xs: Tr.List): nat
    reads *
    decreases {:global} Tr.Size(xs) + Weight
  {
    assert false; t.F(xs)
  }

  // R reaches a dispatched call, so it may not appear in a bound, but an ordinary decreases clause may call it:
  // the call R(t, xs) runs under the caller's bound, and its own bound, Size(xs) + 1, is not larger.
  function R(t: Tr.T, xs: Tr.List): nat
    reads *
    decreases {:global} Tr.Size(xs) + 1
  {
    assert false; t.F(xs)
  }

  function Ordinary(t: Tr.T, xs: Tr.List): nat
    reads *
    decreases {:global} Tr.Size(xs) + 1
    decreases R(t, xs)
  {
    assert false; t.F(xs)
  }
}

module Impl {
  import Tr
  class C extends Tr.T {
    var other: Tr.T
    constructor (other: Tr.T) { this.other := other; }
    function F(xs: Tr.List): nat
      reads *
    {
      assert false;
      match xs
      case Nil => 0
      case Cons(_, tail) => other.F(tail) + 1
    }
  }
}
