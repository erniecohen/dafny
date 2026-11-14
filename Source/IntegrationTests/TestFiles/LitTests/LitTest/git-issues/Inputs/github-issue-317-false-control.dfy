// Every call marked FAIL has a termination check that must fail, with --trait-rank-decreases and
// --additional-axioms on as well as off.  The other calls descend into a component and verify when
// both options are on.

module NonStructural {
  trait T {
    function f(): nat decreases this
  }

  // "this" viewed as a T has the same rank as "this": not a decrease.
  datatype C extends T = C(a: T) {
    function f(): nat decreases this {
      var t: T := this;
      t.f() // FAIL
    }
  }
}

module TwoDatatypeCycle {
  trait T {
    function f(): nat decreases this
  }

  // P.f calls f on a Q that contains this P: the rank goes up, P(..) < Q(P(..)).
  datatype P extends T = P(q: T) {
    function f(): nat decreases this {
      Q(this).f() // FAIL
    }
  }

  datatype Q extends T = Q(p: T) {
    function f(): nat decreases this {
      p.f()
    }
  }
}

module NotAComponent {
  trait T {
    function f(other: T): nat decreases this
  }

  // "other" is not a component of "this".
  datatype C extends T = C(a: T) {
    function f(other: T): nat decreases this {
      other.f(a) // FAIL
    }
  }
}

module ClassRoute {
  trait T {
    function f(): nat decreases this
  }

  datatype C extends T = C(a: T) {
    function f(): nat decreases this {
      a.f()
    }
  }

  // A class value in C's field is below C, but its own measure is a reference, which a datatype
  // is not comparable with: it cannot call back into the datatype that holds it.
  class O extends T {
    const back: C
    constructor (back: C) {
      this.back := back;
    }
    function f(): nat decreases this {
      back.f() // FAIL
    }
  }
}

module CodatatypeRoute {
  trait T {
    function f(): nat decreases this
  }

  datatype C extends T = C(a: T) {
    function f(): nat decreases this {
      a.f()
    }
  }

  // A codatatype value has no rank: it is not comparable with an inductive datatype.
  codatatype S extends T = S(back: C) {
    function f(): nat decreases this {
      back.f() // FAIL
    }
  }

  // Nor with a trait value.
  codatatype S2 extends T = S2(next: T) {
    function f(): nat decreases this {
      next.f() // FAIL
    }
  }
}

module Vacuity {
  trait T {
    function size(): nat decreases this
  }

  datatype L extends T = Nil | Cons(next: T) {
    function size(): nat decreases this {
      match this
      case Nil => 0
      case Cons(next) => 1 + next.size()
    }
  }

  method AssertFalse(x: L) {
    var n := x.size();
    var m := Cons(x).size();
    assert false; // FAIL
  }

  function Bad(x: L): nat decreases x ensures false { // FAIL: postcondition
    if x.Cons? && x.next is L then Bad(x.next as L) else 0
  }
}
