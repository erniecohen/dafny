module Implementation {
  import Slots
  class C extends Slots.T {
    function Apply(c: bounded<nat, int -> int>, x: int): int {
      c(x)
    }
  }
  method Use(c: bounded<nat, int -> int>, x: int)
    decreases {:global} c.bound + 2
  {
    var implementation: Slots.T := new C;
    var y := implementation.Apply(c, x);
    // CONTROL
  }
}
