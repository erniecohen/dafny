// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105): an invariant is well formed
// where the invariants before it in its type's constraint hold, as successive requires clauses are:
// those its type inherits, then its own earlier ones (sections 4.1 and 4.4 of
// docs/dev/issue105-member-invariants-plan.md).  It may not rely on the invariants after it, nor on
// those of a type that extends its own.

module Earlier {
  datatype D = D(n: int) {
    invariant n != 0
    invariant 100 / n > -200
  }
  class C {
    const n: int
    invariant n != 0
    invariant 100 / n > -200
    constructor () { n := 1; }
  }
  trait P extends object {
    const n: int
    invariant n != 0
  }
  class E extends P {
    invariant 100 / n > -200
    constructor () { n := 1; }
  }
  trait Q extends P {
    invariant 100 / n > -300
  }
  trait G<X> {
    const k: int
    invariant k != 0
  }
  datatype B<Y> extends G<Y> = B(y: Y) {
    invariant 100 / k > -200
  }
  trait H<X> extends G<X> {
    invariant 100 / k > -300
  }
  class F<Z> extends H<Z> {
    invariant 100 / k > -400
    constructor () { k := 1; }
  }
  datatype I extends H<int> = I(z: int) {
    invariant 100 / k > -500
  }
}

module Later {
  datatype D = D(n: int) {
    invariant 100 / n > -200                           // error: the invariant after it is not assumed
    invariant n != 0
  }
  trait P extends object {
    const n: int
    invariant 100 / n > -200                           // error: the extending class's invariant is not assumed
  }
  class C extends P {
    invariant n != 0
    constructor () { n := 1; }
  }
}
