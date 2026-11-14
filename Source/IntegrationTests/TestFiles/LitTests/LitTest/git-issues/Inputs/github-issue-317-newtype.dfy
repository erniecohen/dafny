// The call marked FAIL has a termination check that must fail, with --trait-rank-decreases and
// --additional-axioms on as well as off.  The other call descends into a component and verifies when
// both options are on.  A newtype that extends a trait needs --general-traits=full.

module NewtypeRoute {
  trait T {
    function f(): nat decreases this
  }

  datatype C extends T = C(a: T) {
    function f(): nat decreases this {
      a.f()
    }
  }

  // A newtype value has no rank: C(this) holds it, and C.f calls back into it.
  newtype N extends T = x: int | true {
    function f(): nat decreases this {
      C(this).f() // FAIL
    }
  }
}
