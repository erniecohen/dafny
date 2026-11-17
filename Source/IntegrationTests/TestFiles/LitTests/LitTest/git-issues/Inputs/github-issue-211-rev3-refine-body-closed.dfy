// As github-issue-211-rev3-refine-body-ok.dfy, but the refinement's body of the {:termination_closed} K reaches a
// dispatch, which breaks the promise and is rejected.
module Lib {
  trait {:termination_closed} T extends object { ghost predicate p() decreases {:global} 1 }
}
abstract module A {
  import Lib
  ghost function {:termination_closed} K(t: Lib.T): bool
  ghost predicate Low(t: Lib.T) decreases {:global} 0 { K(t) }
}
module B refines A {
  ghost function {:termination_closed} K(t: Lib.T): bool { t.p() }
}
