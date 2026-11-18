// RUN: %exits-with 0 %baredafny build --target:lib --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false "%s" --output "%t.doo" > "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --library "%t.doo" "%S/Inputs/github-issue-371-client.dfy" >> "%t"
// RUN: %exits-with 0 %baredafny audit --use-basename-for-filename --standard-libraries:false "%s" >> "%t"
// RUN: %exits-with 3 %baredafny build --target:lib --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false "%S/Inputs/github-issue-371-user.dfy" --output "%t-user.doo" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Under --modular-termination, a library may declare an opaque const
// (https://github.com/erniecohen/dafny/issues/371).  Dafny generates a reveal lemma for it that
// states the constant's definition, with no body and no {:axiom}.  That lemma is recognised as
// generated, like the reveal lemmas of opaque functions, so neither the build nor 'dafny audit'
// counts it as a bodyless member with a postcondition: in a concrete module (Concrete), and in the
// copies that a refinement makes of an abstract module's (Base, refined by Leaf).  A bodyless lemma
// with a postcondition that the user writes is still refused (Inputs/github-issue-371-user.dfy).

module Concrete {
  opaque const K: nat := 5

  class Cell {
    static opaque const S: nat := 7
    opaque const I: nat := 9
  }

  lemma KIs()
    ensures K == 5
  {
    reveal K;
  }

  lemma CellIs(c: Cell)
    ensures Cell.S == 7 && c.I == 9
  {
    reveal Cell.S, c.I;
  }
}

abstract module Base {
  opaque const B: nat := 2

  class Box {
    opaque const J: nat := 4
  }
}

module Leaf refines Base {
  lemma BIs()
    ensures B == 2
  {
    reveal B;
  }

  lemma BoxIs(b: Box)
    ensures b.J == 4
  {
    reveal b.J;
  }
}
