// Part of https://github.com/erniecohen/dafny/issues/404 (see ../github-issue-404.dfy): a refining module
// that changes neither member of an inherited override pair does not check the pair again.  This file
// verifies as many tasks by default as under --legacy-elided-override-checks.

module Base {
  trait S { lemma L() ensures true }
  class C extends S { constructor () {} lemma L() ensures true { } }
  method Other() { }
}

// Both declarations are refined, but neither member of the pair changes.
module Mentioned refines Base {
  trait S ... { lemma Extra() { } }
  class C ... { lemma More() { } }
  method Other() { assert true; }
}

// Nothing is mentioned, along a chain of refinements.
module Copied1 refines Base { }
module Copied2 refines Copied1 {
  method New() { var s: S := new C(); s.L(); }
}

// The trait lies in a module that is not refined.
module Library {
  trait {:termination false} {:axiom} S { lemma L() ensures true }
}
module OutsideBase {
  import Library
  class C extends Library.S { constructor () {} lemma L() ensures true { } }
}
module Outside refines OutsideBase {
  class C ... { }
}
