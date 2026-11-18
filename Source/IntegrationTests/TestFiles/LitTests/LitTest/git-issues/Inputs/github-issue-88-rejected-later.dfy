// Programs that --refinement-extends rejects in the later phases of resolution.

// ----- A member of the added parent captures a name that inherited text used

module Capture0 {
  ghost predicate P() { true }
  trait T extends object {
    lemma L() ensures P() {}
  }
}
module Capture1 refines Capture0 {
  trait S extends object {
    ghost predicate P() { false }
  }
  trait T ... extends S {} // error: in T.L, P would now be S.P
}

// ----- Type arguments of the added parent

module Arguments0 {
  trait Twice extends object {}
}
module Arguments1 refines Arguments0 {
  trait S<Y> extends object {}
  trait Twice ... extends S<int>, S<bool> {} // error: two instantiations of S
}
