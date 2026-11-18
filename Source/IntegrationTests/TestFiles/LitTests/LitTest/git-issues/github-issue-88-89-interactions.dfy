// RUN: %exits-with 4 %verify --inherited-trait-axioms --refinement-extends --refinement-renaming --copy-imports "%s" > "%t"
// RUN: %exits-with 4 %verify --inherited-trait-axioms --refinement-extends --refinement-renaming --copy-imports --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 2 %resolve --inherited-trait-axioms --refinement-renaming "%S/Inputs/github-issue-88-89-interactions-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --inherited-trait-axioms --refinement-renaming --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-88-89-interactions-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --inherited-trait-axioms --refinement-renaming "%S/Inputs/github-issue-88-89-function-type-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --inherited-trait-axioms --refinement-renaming --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-88-89-function-type-rejected.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Renamed type parameters must reach inherited parents, specifications and axiom members.
module Original {
  trait Parent<A> {
    ghost function {:axiom} Identity(a: A): A
      ensures Identity(a) == a
    lemma {:axiom} Same(a: A) ensures Identity(a) == a
  }
  trait Child<A> {}
}
module Renamed refines Original {
  trait Parent<B> ... {
    ghost function Identity(b: B): B
    lemma Same(b: B)
  }
  trait Child<B> ... extends Parent<B> {}
  class Concrete extends Child<int> {}
  lemma Positive(c: Concrete, x: int) {
    assert c.Identity(x) == x;
    c.Same(x);
  }
  lemma Negative(c: Concrete, x: int) {
    assert c.Identity(x) == x;
    c.Same(x);
    assert false;
  }
}

// Copying a refined module and then refining its importer must keep the inherited axiom status
// and the added parent. Each copy has its own nominal types, but the same usable contracts.
module Copies {
  import First := Renamed
  import Second := Renamed
  lemma Positive(c: First.Concrete, d: Second.Concrete) {
    assert c.Identity(3) == 3;
    d.Same(4);
    assert d.Identity(4) == 4;
    var p: First.Parent<int> := c;
    assert p.Identity(5) == 5;
  }
  lemma Negative(c: First.Concrete) {
    c.Same(3);
    assert c.Identity(3) == 3;
    assert false;
  }
}
module RefinedCopies refines Copies {
  lemma Again(c: First.Concrete, d: Second.Concrete) { Positive(c, d); }
  lemma NegativeAgain(c: First.Concrete, d: Second.Concrete) {
    Positive(c, d);
    assert false;
  }
}
