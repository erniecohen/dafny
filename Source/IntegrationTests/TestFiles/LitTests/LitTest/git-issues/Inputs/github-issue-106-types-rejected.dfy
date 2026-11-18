// With --refinement-renaming, the types of a refining signature must still be the types of the
// signature it refines.  A subset type and a newtype are types of their own.  Each error below
// is reported with the option; without it, every difference in spelling is an error.

abstract module A {
  type T
  predicate P(t: T)
  function Subset(t: T): T
  function Ints(x: int): int
  function Collections(s: seq<T>): T
  function Permuted<X, Y>(a: X, b: Y): X
  function Result(t: T): T
  method Outs(t: T) returns (u: T, n: int)
  class C {
    constructor (t: T)
  }
}

module B refines A {
  datatype Node = Leaf | Cons(Node)
  type T = Node
  type Small = t: T | P(t) witness *
  newtype Num = x: int | true

  function Subset(t: Small): T  // error: a subset type of T is not T
  function Ints(x: nat): Num  // error (x2): nat is not int, and a newtype over int is not int
  function Collections(s: set<Node>): Node  // error: set is not seq
  function Permuted<Y, X>(a: X, b: Y): X  // error (x3): the type parameters are renamed by position
  function Result(t: Node): (r: bool)  // error: bool is not T
  method Outs(t: Node) returns (u: Small, n: Num)  // error (x2)
  class C ... {
    constructor (t: bool)  // error: bool is not T
  }
}

// A type that the refining module sees only as an opaque type is not known to be another type.
module Lib {
  export provides U, V
  type U = int
  type V = int
}

abstract module A2 {
  import Lib
  function F(u: Lib.U): Lib.U
}

module B2 refines A2 {
  function F(u: Lib.V): Lib.U  // error: Lib.V is not known to be Lib.U
}

// The comparison by type is for the parameters and results of functions, methods, lemmas and
// constructors.  The type of a const and a type bound must still be spelled the same.
abstract module A3 {
  type T
  trait Tr { }
  const c: T
  function Bound<X extends Tr>(x: X): X
}

module B3 refines A3 {
  datatype Node = Leaf
  type T = Node
  type Tr' = Tr
  const c: Node := Leaf  // error: the type of a const must be spelled the same
  function Bound<X extends Tr'>(x: X): X  // error: so must a type bound
}

// A type that does not resolve is reported as such.
abstract module A4 {
  type T
  function F(t: T): T
}

module B4 refines A4 {
  type T = int
  function F(t: Undeclared): T  // error: Undeclared is not a type
}
