// Recursion across module boundaries (https://github.com/erniecohen/dafny/issues/466), second
// stage: what a recursive module group may not have yet.  Each needs a rule of its own, which a
// later stage adds, so until then each is an error and not an unchecked acceptance: an abstract
// member, a member that refines or replaces a module, an abstract import in a member, an invariant
// in a member, and a module outside the group that refines a member or names one in an
// abstract import.  (A type that extends a trait of another member was in this list until the
// third stage; see git-issue-466-stage3-termination.dfy.)
// RUN: %exits-with 2 %resolve --member-invariants --recursive-modules "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// an abstract member
abstract module Abstract1 {
  import Abstract2
  type T
  function F(t: T): Abstract2.U
}
abstract module Abstract2 {
  import Abstract1
  type U = int
}

// a member that refines a module
module Spec {
  function F(): int
}
module Refining1 refines Spec {
  import Refining2
  function F(): int { Refining2.G() }
}
module Refining2 {
  import Refining1
  function G(): int { 0 }
}

// a member that replaces a module
replaceable module Service {
  function F(): int
}
module Replacing1 replaces Service {
  import Replacing2
  function F(): int { Replacing2.G() }
}
module Replacing2 {
  import Replacing1
  function G(): int { 0 }
}

// an abstract import in a member
abstract module Importing1 {
  import Importing2
  import X : Spec
}
module Importing2 {
  import Importing1
}

// an invariant in a member
module Invariant1 {
  import Invariant2
  datatype Range = Range(lo: int, hi: int) {
    invariant lo <= hi
  }
}
module Invariant2 {
  import Invariant1
  function Width(r: Invariant1.Range): int { r.hi - r.lo }
}

// modules outside a group that refine a member and name one in an abstract import (a replaceable
// module is never in a group, so no module replaces a member)
module Plain1 {
  import Plain2
  function F(): int { Plain2.G() }
}
module Plain2 {
  import Plain1
  function G(): int { 1 }
  function K(): int { Plain1.F() }
}
module Refiner refines Plain1 {
  lemma L()
    ensures F() == 1
  {
  }
}
abstract module User {
  import X : Plain2
  lemma L()
    ensures X.G() == 1
  {
  }
}
