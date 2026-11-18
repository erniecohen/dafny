// Part of https://github.com/erniecohen/dafny/issues/89 (see ../github-issue-89.dfy): without
// --inherited-trait-axioms a refinement may add {:axiom} to a bodiless trait member, as before.  With the
// option, each of the two is an error.

module Base {
  trait T {
    lemma L()
    ghost function F(): int
  }
}

module AddsAxiom refines Base {
  trait T ... {
    lemma {:axiom} L... ensures 0 < 1  // error, with the option
    ghost function {:axiom} F...  // error, with the option
  }
}
