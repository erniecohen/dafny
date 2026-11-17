// Part of https://github.com/erniecohen/dafny/issues/404 (see ../github-issue-404.dfy): the override
// check of an inherited datatype or newtype member, under --general-traits=full.  Each method M proved
// false before.  It fails by default and verifies under --legacy-elided-override-checks.

// A datatype member.
module DatatypeBase {
  trait S { lemma L() ensures true }
  datatype D extends S = D0 { lemma L() ensures true { } }
}
module Datatype refines DatatypeBase {
  trait S ... { lemma L... ensures false }
  method M() ensures false { var s: S := D0; s.L(); }
}

// A newtype member.
module NewtypeBase {
  trait S { lemma L() ensures true }
  newtype N extends S = int { lemma L() ensures true { } }
}
module Newtype refines NewtypeBase {
  trait S ... { lemma L... ensures false }
  method M() ensures false { var s: S := 0 as N; s.L(); }
}

// An abstract type refined into a datatype that adds a parent.
module AbstractTypeBase {
  trait S { lemma L() ensures false }
  type T {
    lemma L() ensures true { }
  }
}
module AbstractType refines AbstractTypeBase {
  datatype T extends S = T0
  method M() ensures false { var s: S := T0; s.L(); }
}
