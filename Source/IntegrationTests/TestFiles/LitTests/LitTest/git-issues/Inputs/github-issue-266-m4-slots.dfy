// A checked library slot whose future implementations receive persistent bounded evidence.
module Slots {
  trait T {
    function Apply(c: bounded<nat, int -> int>, x: int): int
      decreases {:global} c.bound + 1
  }
}
