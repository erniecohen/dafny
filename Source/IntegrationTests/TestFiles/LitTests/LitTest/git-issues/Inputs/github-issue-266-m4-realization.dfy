abstract module Signature {
  function Apply(c: bounded<nat, int -> int>, x: int): int
    decreases {:global} c.bound + 1
}

module Realization refines Signature {
  function Apply(c: bounded<nat, int -> int>, x: int): int {
    c(x)
  }
}

abstract module Client {
  import S : Signature
  function Use(c: bounded<nat, int -> int>, x: int): int
    decreases {:global} c.bound + 2
  {
    S.Apply(c, x)
  }
}

module Bound refines Client {
  import S = Realization
  method Control(c: bounded<nat, int -> int>, x: int)
    decreases {:global} c.bound + 3
  {
    var y := Use(c, x);
    // CONTROL
  }
}
