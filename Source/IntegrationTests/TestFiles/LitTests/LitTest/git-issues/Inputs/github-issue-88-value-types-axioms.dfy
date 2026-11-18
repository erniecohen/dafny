// With --inherited-trait-axioms, a value type that gains a parent in a refinement inherits the
// parent's axioms, as a class does.

module Axiom0 {
  datatype D = X {
    ghost predicate P() {
      true
    }
  }
  datatype E = E0
}

module Axiom1 refines Axiom0 {
  trait S {
    ghost predicate P()
    ghost function Q(): int
    lemma {:axiom} L()
      ensures P() ==> Q() == 3
  }
  datatype D extends S ... {
    ghost function Q(): int
  }

  method M(d: D) {
    d.L();
    assert d.Q() == 3;
    assert false; // error
  }
}

module Axiom2 refines Axiom0 {
  trait S {
    lemma {:axiom} L()
      ensures false
  }
  datatype E extends S ... {}

  method M(e: E) {
    assert false; // error: the axiom has not been used
  }

  method Trusted(e: E)
    ensures false
  {
    e.L(); // the axiom is trusted, here as in a class
  }
}
