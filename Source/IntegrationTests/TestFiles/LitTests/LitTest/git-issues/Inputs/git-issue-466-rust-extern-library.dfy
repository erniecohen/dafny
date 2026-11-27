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
  const End: int := 7
  const Start: int := B.Middle + Identity(1)
  function Count(n: nat): nat { if n == 0 then 0 else Identity(1) + NativeOnly.Zero() + B.Count(n - 1) }
}
module {:extern "Foreign.Types"} B {
  class {:extern "Actual"} Native {
    constructor {:extern} ()
    function {:extern "Read"} Value(): int
    function Local(): int { Count(3) }
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
    function {:extern "ExternalEval"} ForeignEval(): int
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
  class {:extern} Opaque { constructor {:extern} () }
  function {:extern} Zero(): (r: nat) ensures r == 0
}

module NativeClient {
  import NativeOnly
  function Zero(): nat { NativeOnly.Zero() }
}

// Abstract refinements emit no foreign imports or copied implementations.
abstract module Ignored refines NativeOnly { }
