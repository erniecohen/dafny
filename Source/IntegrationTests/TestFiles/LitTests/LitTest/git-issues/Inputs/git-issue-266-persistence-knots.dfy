// Each knot has a certified implementation and well-formed storage. The only
// missing premise is strict progress at the indicated application. Allowing
// equality there would close High -> Use -> callback -> High.

module DatatypeKnot {
  datatype Argument = Argument(owner: C)
  datatype Storage = Storage(callback: Zero)
  type Zero = c: bounded<nat, Argument -> int> | c.bound == 0
    witness bounded.certify(0, (a: Argument) => 0)

  class C {
    const stored: Storage
    constructor (c: Zero) ensures stored == Storage(c) { stored := Storage(c); }
    function Use(): int
      decreases {:global} 0
    { stored.callback(Argument(this)) } // error: 0 < 0
  }
  function High(a: Argument): int
    decreases {:global} 0
  { a.owner.Use() }

  method Install() {
    var callback := bounded.certify(0, (a: Argument) => High(a));
    var owner := new C(callback);
    var result := owner.Use();
  }
}

module ConstantKnot {
  type Zero = c: bounded<nat, Argument -> int> | c.bound == 0
    witness bounded.certify(0, (a: Argument) => 0)
  class Argument {
    const owner: C
    constructor (c: C) ensures owner == c { owner := c; }
  }
  class Storage {
    const callback: Zero
    constructor (c: Zero) ensures callback == c { callback := c; }
  }
  class C {
    const stored: Storage
    constructor (c: Zero)
      ensures stored.callback == c
    { stored := new Storage(c); }
    function Use(a: Argument): int
      decreases {:global} 0
    { stored.callback(a) } // error: 0 < 0
  }
  function High(a: Argument): int
    decreases {:global} 0
  { a.owner.Use(a) }

  method Install() {
    var callback := bounded.certify(0, (a: Argument) => High(a));
    var owner := new C(callback);
    var argument := new Argument(owner);
    var result := owner.Use(argument);
  }
}

module ReturnedBound {
  datatype Storage = Storage(callback: bounded<nat, int -> int>)
  function Return(s: Storage): bounded<nat, int -> int> { s.callback }

  function SameBound(s: Storage, x: int): int
    requires s.callback.bound == 1
    decreases {:global} 1
  { Return(s)(x) } // error: returning a bound does not lower it

  ghost function GhostReturn(s: Storage): bounded<nat, int -> int> { s.callback }

  lemma SameBoundGhost(s: Storage)
    requires s.callback.bound == 1
    decreases {:global} 1
  {
    ghost var result := GhostReturn(s)(0); // error: 1 < 1
  }
}
