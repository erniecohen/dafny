// Milestone M4a of --modular-termination (https://github.com/erniecohen/dafny/issues/211): bounds that use
// only code that cannot reach a dispatched call are accepted: a recursive function over a datatype, a function
// that reads the heap, a lambda with such a body, a subset type with such a constraint, a constant with such an
// initializer.  A function that does reach a dispatched call may still be used outside the bounds, here in an
// ordinary decreases clause, where the same-bound rule checks its call.

module Tr {
  datatype {:termination_closed} List = Nil | Cons(head: nat, tail: List)

  function {:termination_closed} Size(xs: List): nat {
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

  function Fuel(t: Tr.T): nat reads t { t.fuel }

  function ViaDatatype(t: Tr.T, xs: Tr.List): nat
    reads *
    decreases {:global} Tr.Size(xs) + 1
  {
    t.F(xs)
  }

  method ViaHeap(t: Tr.T, xs: Tr.List) returns (r: nat)
    decreases {:global} Fuel(t) + Tr.Size(xs) + 1
  {
    r := t.F(xs);
  }

  function ViaLambda(t: Tr.T, xs: Tr.List): nat
    reads *
    decreases {:global} ((ys: Tr.List) => Tr.Size(ys) + 1)(xs)
  {
    t.F(xs)
  }

  function ViaSubsetType(t: Tr.T, xs: Tr.List, s: Tr.Small): nat
    reads *
    decreases {:global} Tr.Size(xs) + (if s is Tr.Small then 1 else 2)
  {
    t.F(xs)
  }

  function ViaConstant(t: Tr.T, xs: Tr.List): nat
    reads *
    decreases {:global} Tr.Size(xs) + Weight
  {
    t.F(xs)
  }

  // R reaches a dispatched call, so it may not appear in a bound, but an ordinary decreases clause may call it:
  // the call R(t, xs) runs under the caller's bound, and its own bound, Size(xs) + 1, is not larger.
  function R(t: Tr.T, xs: Tr.List): nat
    reads *
    decreases {:global} Tr.Size(xs) + 1
  {
    t.F(xs)
  }

  function Ordinary(t: Tr.T, xs: Tr.List): nat
    reads *
    decreases {:global} Tr.Size(xs) + 1
    decreases R(t, xs)
  {
    t.F(xs)
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
      match xs
      case Nil => 0
      case Cons(_, tail) => other.F(tail) + 1
    }
  }
}
