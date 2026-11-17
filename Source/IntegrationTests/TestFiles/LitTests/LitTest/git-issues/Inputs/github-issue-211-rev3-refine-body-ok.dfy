// The control for github-issue-211-rev3-refine-body.dfy.  A bodiless function that declares a bound, or promises
// {:termination_closed}, may be called below that bound, and a refinement that gives it a body is checked
// against its promise.  The only error is the 'assert false' control at the end.
module Lib {
  trait {:termination_closed} T extends object { ghost predicate p() decreases {:global} 1 }
}
abstract module A {
  import Lib
  ghost function U(t: Lib.T): bool decreases {:global} 2
  ghost function {:termination_closed} K(n: nat): nat
  ghost predicate Low(t: Lib.T) decreases {:global} 3 { U(t) && K(0) == 0 }
}
module B refines A {
  ghost function U(t: Lib.T): bool { !t.p() }
  ghost function {:termination_closed} K(n: nat): nat { 0 }
}
module Cl {
  import B
  import Lib
  class C extends Lib.T { constructor () {} ghost predicate p() { false } }
  method Main() {
    var c := new C();
    assert B.Low(c);
    assert false;
  }
}
