// A refining module gives a body to a function that its base left without one.  In the abstract module A, Low,
// bounded at 0, calls U, which has no body there; in B, U's body dispatches to p, whose implementation C.p calls
// back into B.Low.  B inherits Low and its checked call, so the call must be rejected in A: a member without a
// body, unless it promises {:termination_closed}, is taken to reach a dispatch.  Were it accepted,
// B.Low(c) == !B.Low(c), and Main would prove false: its body verifies.
module Lib {
  trait {:termination_closed} T extends object { ghost predicate p() decreases {:global} 1 }
}
abstract module A {
  import Lib
  ghost function U(t: Lib.T): bool
  ghost predicate Low(t: Lib.T) decreases {:global} 0 { U(t) }
}
module B refines A {
  ghost function U(t: Lib.T): bool { !t.p() }
}
module Cl {
  import B
  import Lib
  class C extends Lib.T { constructor () {} ghost predicate p() { B.Low(this) } }
  method Main() ensures false { var c := new C(); assert B.Low(c) == B.U(c) == !c.p(); }
}
