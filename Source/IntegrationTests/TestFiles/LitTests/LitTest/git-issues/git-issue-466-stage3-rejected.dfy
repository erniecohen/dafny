// Recursion across module boundaries (https://github.com/erniecohen/dafny/issues/466), third
// stage: programs with traits in recursive module groups that the resolver rejects.  Each is
// rejected for the reason its declarations would be rejected in one module, or because an export
// set hides what the declaration needs, or (the last two inputs) because the parent traits of a
// trait are named in a way that cannot be looked up before the imports of the group are resolved.
// RUN: %exits-with 2 %resolve --member-invariants --recursive-modules "%s" > "%t"
// RUN: %exits-with 2 %resolve --member-invariants --recursive-modules "%S/Inputs/git-issue-466-stage3-cycle.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --member-invariants --recursive-modules "%S/Inputs/git-issue-466-stage3-reference.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --member-invariants --recursive-modules "%S/Inputs/git-issue-466-stage3-opaque.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --member-invariants --recursive-modules "%S/Inputs/git-issue-466-stage3-lookup1.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --member-invariants --recursive-modules "%S/Inputs/git-issue-466-stage3-lookup2.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --member-invariants --recursive-modules "%S/Inputs/git-issue-466-stage3-above.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// a class that does not implement a member of the trait of another member
module Missing1 {
  import Missing2
  trait T {
    function F(): nat
    lemma L()
      ensures false
  }
  lemma Use(c: Missing2.C)
    ensures false
  {
    c.L();
  }
}
module Missing2 {
  import Missing1
  class C extends Missing1.T {
    function F(): nat { 0 }
  }
}

// the same where the export set of the trait's module hides the member
module Hidden1 {
  export
    reveals T
    provides T.F, Use, Hidden2
  import Hidden2
  trait T {
    function F(): nat
    lemma L()
      ensures false
  }
  lemma Use(c: Hidden2.C)
    ensures false
  {
    c.L();
  }
}
module Hidden2 {
  import Hidden1
  class C extends Hidden1.T {
    function F(): nat { 0 }
  }
}

// overrides that do not match the members of the trait of another member
module Mismatch1 {
  import Mismatch2
  trait T {
    function F(n: nat): nat
    method M(n: nat) returns (r: nat)
    ghost function G(): nat
    function H(): nat { 0 }
  }
  function Use(c: Mismatch2.C): nat { c.F(0) }
}
module Mismatch2 {
  import Mismatch1
  class C extends Mismatch1.T {
    function F(n: int): nat { 0 }
    method M(n: nat) returns (r: int) { r := 0; }
    function G(): nat { 0 }
    function H(): nat { 1 }
  }
}
