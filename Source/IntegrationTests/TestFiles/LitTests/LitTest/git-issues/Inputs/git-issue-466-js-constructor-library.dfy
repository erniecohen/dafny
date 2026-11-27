// A three-member group, with no module order that orders all the constants.
module A {
  import NativeOnly
  function {:extern "Foreign.Tools", "Identity"} Identity(x: int): (r: int)
    ensures r == x
  import B
  datatype Tree<X> = Leaf(value: X) | Node(child: B.Branch<X>)
  datatype Path = Next(edge: B.Edge) | Stop
  datatype Positive = Positive(n: int) {
    invariant n > 0
    witness Positive(Identity(2))
  }
  trait Counter {
    function Eval(n: nat): nat
  }
  function {:extern} LocalBody(): int { 3 }
  method {:extern "Foreign.Tools", "Increment"} Increment(x: int) returns (y: int)
    ensures y == x + 1
  const End: int := 7
  const Start: int := B.Middle + Identity(1)
  function Count(n: nat): nat { if n == 0 then 0 else Identity(1) + NativeOnly.Zero() + B.Count(n - 1) }
}
module {:extern "ForeignB"} B {
  class {:extern "NativeCell"} Cell<T(0)> {
    constructor {:extern "Init"} Init(ghost erased: int, seed: T, tag: int)
      ensures Read() == seed && Tag() == tag
    function {:extern} Read(): T
    function {:extern} Tag(): int
    function {:extern} InitialValue(): T
  }
  class {:extern "NativeInt"} IntCell {
    constructor {:extern} (ghost erased: int, value: int)
      ensures Read() == value
    function {:extern} Read(): int
  }
  class {:extern "NativeZero"} Zero {
    constructor {:extern} (ghost erased: int)
    function {:extern} Read(): int
  }
  class {:extern "Actual"} Native {
    constructor () { }
    function {:extern "Read"} Value(): int
    function Local(): int { Count(3) }
    var {:extern "number"} Number: int
    method {:extern "Increment"} Add(x: int) returns (y: int)
      ensures y == x + 1
  }

  class {:extern} Handle {
    static function {:extern "Make"} Create(): Handle
    function {:extern "Read"} Value(): int
  }

  import C
  import A
  function {:extern "Foreign.Tools", "Echo"} Echo(x: A.Tree<int>): (r: A.Tree<int>)
    ensures r == x
  datatype Branch<X> = Branch(tree: A.Tree<X>)
  datatype Edge = Edge(path: A.Path)
  datatype Box = Box(p: A.Positive)
  class Implementation extends A.Counter {
    constructor () { }
    function Eval(n: nat): nat { C.Count(n) + A.Identity(0) }
    function {:extern "Foreign.Tools", "ExternalEval"} ForeignEval(): int
  }
  const Middle: int := C.Last + 1
  function Count(n: nat): nat { if n == 0 then 0 else 1 + C.Count(n - 1) }
}
module C {
  import A
  const Last: int := A.Identity(A.End + 1)
  function Count(n: nat): nat { if n == 0 then 0 else 1 + A.Count(n - 1) }
}

// Refinement retains the original siblings; it does not tie the group again.
module Refined refines A { }

// Abstract groups contribute no executable declarations.
abstract module AbstractA {
  import AbstractB
  datatype T = Leaf | Node(u: AbstractB.U)
}
abstract module AbstractB {
  import AbstractA
  datatype U = U(t: AbstractA.T)
}

module {:extern} NativeOnly {
  import NativeClient
  function {:extern} Zero(): (r: nat) ensures r == 0
}

module NativeClient {
  import NativeOnly
  function Zero(): nat { NativeOnly.Zero() }
}
