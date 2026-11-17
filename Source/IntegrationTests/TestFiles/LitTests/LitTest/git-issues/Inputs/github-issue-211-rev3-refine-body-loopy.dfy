// As github-issue-211-rev3-refine-body-ok.dfy, but the refinement's body of U, bounded at 0, dispatches to p, at 1,
// which is rejected.
module Lib {
  trait {:termination_closed} T extends object { ghost predicate p() decreases {:global} 1 }
}
abstract module A {
  import Lib
  ghost function U(t: Lib.T): bool decreases {:global} 0
  ghost predicate Low(t: Lib.T) decreases {:global} 0 { U(t) }
}
module B refines A {
  ghost function U(t: Lib.T): bool { !t.p() }
}
