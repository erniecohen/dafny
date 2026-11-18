// With --refinement-renaming, a refining signature may spell the type of a parameter or of the
// result differently from the signature it refines, as long as both spellings denote the same
// type.  Type synonyms are expanded for the comparison.  The merged declaration keeps the type
// of the refined signature.

// An abstract type that a synonym refines: the refining module may use the new name.
abstract module A {
  type T(==)

  function F(t: T): T

  predicate P(t: T)

  method M(t: T) returns (u: T)
    ensures u == t

  lemma L(t: T, s: seq<T>)
    ensures F(t) == t ==> |s| >= 0

  function Named(t: T): (r: T)
    ensures r == t

  function Unnamed(t: T): T

  function WithDefault(t: T, u: T := t): T {
    u
  }

  function Nested(m: map<T, set<T>>, f: T -> seq<T>, p: (T, T), a: array<T>): T ~> multiset<T>

  twostate function Two(t: T, new u: T): T

  least predicate Reach(t: T)

  least lemma Reached(t: T)
    requires Reach(t)
    ensures F(t) == t ==> Reach(F(t))

  class C {
    var t: T
    constructor (t0: T)
      ensures t == t0
    method Get() returns (r: T)
      ensures r == t
    static function Id(t: T): T
  }
}

module B refines A {
  datatype Node = Leaf | Cons(next: Node)
  type T = Node
  type Nodes = seq<Node>

  function F(t: Node): Node {
    t
  }

  predicate P(t: Node) {
    t.Leaf?
  }

  method M(t: Node) returns (u: Node) {
    u := t;
  }

  lemma L(t: Node, s: Nodes) {
  }

  function Named(t: Node): (r: Node) {
    t
  }

  // the refinement names the result, and still inherits the result type
  function Unnamed(t: Node): (r: Node)
    ensures r == t
  {
    t
  }

  function WithDefault(t: Node, u: Node): Node

  function Nested(m: map<Node, set<T>>, f: Node -> Nodes, p: (T, Node), a: array<Node>): Node ~> multiset<Node> {
    (n: Node) => multiset{n}
  }

  twostate function Two(t: Node, new u: Node): Node {
    u
  }

  least predicate Reach(t: Node) {
    t.Leaf? || Reach(t.next)
  }

  least lemma Reached(t: Node) {
  }

  class C ... {
    constructor (t0: Node) {
      t := t0;
    }
    method Get() returns (r: Node) {
      r := t;
    }
    static function Id(t: Node): Node {
      t
    }
  }
}

// A further refinement may use either name.
module B' refines B {
  function F(t: T): Node
  method M(t: Node) returns (u: T)
}

// A concrete type that the refining module gives a second name.
module A2 {
  datatype T = T0 | T1
  function F(t: T): T {
    t
  }
}

module B2 refines A2 {
  export
    reveals T, Node
    provides F
  type Node = T
  function F(t: Node): Node
    ensures F(t) == t
}

// Type parameters renamed, and their types spelled through a generic synonym.
abstract module A3 {
  datatype Box<X> = Box(x: X)
  function G<X, Y>(b: Box<X>, m: map<X, Y>): (r: Box<X>) {
    b
  }
  method Swap<X, Y>(x: X, y: Y) returns (y': Y, x': X)
    ensures x' == x && y' == y
}

module B3 refines A3 {
  type Boxed<Z> = Box<Z>
  type Pairs<K, V> = map<K, V>
  function G<U, V>(b: Boxed<U>, m: Pairs<U, V>): (q: Boxed<U>)
    ensures q == b
  method Swap<P, Q>(p: P, q: Q) returns (q': Q, p': P) {
    q', p' := q, p;
  }
}

// An imported type, written with and without its module, and through a revealed synonym.
module Lib {
  export reveals U, W
  type U = int
  datatype W = W(n: int)
}

abstract module A4 {
  import Lib
  function F(u: Lib.U, w: Lib.W): int
}

module B4 refines A4 {
  import opened Other = Lib
  function F(u: int, w: W): U {
    u + w.n
  }
}

module Client {
  import B
  import B'
  import B2
  import B3
  import B4
  import Lib

  method Test() {
    assert B.F(B.Leaf) == B.Leaf;
    assert B.Unnamed(B.Cons(B.Leaf)).next == B.Leaf;
    assert B.WithDefault(t := B.Leaf) == B.Leaf;
    assert B'.F(t := B'.Leaf) == B'.Leaf;
    var n: B.T := B.M(B.Leaf);
    assert n.Leaf?;
    var c := new B.C(n);
    var r: B.Node := c.Get();
    assert r == B.Leaf;
    assert B2.F(B2.T0) == B2.T0;
    var b := B3.G(B3.Box(3), map[3 := true]);
    assert b.x == 3;
    var t, i := B3.Swap(x := 1, y := true);
    assert t && i == 1;
    assert B4.F(2, Lib.W(3)) == 5;
  }
}
