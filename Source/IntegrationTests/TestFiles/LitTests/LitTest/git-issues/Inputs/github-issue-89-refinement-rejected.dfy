// Part of https://github.com/erniecohen/dafny/issues/89 (see ../github-issue-89.dfy): what
// --inherited-trait-axioms rejects in a refinement.

module Base {
  trait T {
    lemma Interface() ensures 0 < 1
    ghost function {:axiom} Axiom(): int
    ghost function {:axiom} Overridden(): int
  }
  class C extends T {
    lemma Interface() ensures 0 < 1 { }
    ghost function Overridden(): int { 1 }
  }
}

// Whether inheriting types must implement a member is fixed where the member is first declared.
module AddsAxiom refines Base {
  trait T ... {
    lemma {:axiom} Interface...  // error
  }
}

// Adding {:axiom} together with a body is fine: the member is no trait axiom then.
module AddsAxiomAndBody refines Base {
  trait T ... {
    lemma {:axiom} Interface... { }
  }
}

// A function axiom that a refinement makes compiled is a bodiless compiled member with {:axiom}.
module MakesCompiled refines Base {
  trait T ... {
    function Axiom(): int  // error
  }
}

// A function axiom is given a body, but an inherited class overrides it: the existing error.
module GivesBody refines Base {
  trait T ... {
    ghost function Overridden(): int { 0 }  // error (reported at C.Overridden)
  }
}

// What a refinement may still do to a bodiless trait member: add {:axiom} to a static member, which no
// type inherits as a member to implement; repeat {:axiom} on a member that is an axiom already; and
// refine a member without adding {:axiom}.
module Allowed0 {
  trait T {
    static lemma Static()
    ghost function {:axiom} Axiom(): int
    ghost function Plain(): int
    lemma PlainLemma()
  }
}
module Allowed1 refines Allowed0 {
  trait T ... {
    static lemma {:axiom} Static... ensures 0 < 1
    ghost function {:axiom} Axiom...
    ghost function Plain...
    lemma PlainLemma... ensures 1 < 2
  }
}
