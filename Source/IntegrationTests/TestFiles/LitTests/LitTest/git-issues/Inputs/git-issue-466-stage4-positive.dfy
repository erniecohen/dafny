module Trees {
  import Forests
  datatype Tree = Leaf(n: int) | Node(children: seq<Forests.Forest>) {
    invariant Leaf? ==> n >= 0
    invariant Node? ==> Forests.All(children)
    predicate Valid() { !Leaf? || n >= 0 }
    witness Leaf(0)
  }
  function Unit(): Tree { Leaf(1) }
  lemma Facts(t: Tree) { assert t.Valid(); }
}
module Forests {
  import Trees
  datatype Forest = Forest(trees: seq<Trees.Tree>) {
    invariant AllTrees(trees)
    witness Forest([])
  }
  ghost predicate AllTrees(ts: seq<Trees.Tree>) { forall t: Trees.Tree | t in ts :: t.Leaf? ==> t.n >= 0 }
  ghost predicate All(fs: seq<Forest>) { forall f <- fs :: AllTrees(f.trees) }
  function Unit(): Forest { Forest([Trees.Unit()]) }
  lemma Facts(f: Forest) { assert AllTrees(f.trees); }
}

module First {
  import Second
  datatype T = End(n: int) | Next(u: Second.U) {
    invariant End? ==> n >= 0
    invariant Next? ==> Second.Good(u)
    witness End(0)
  }
  function Unit(): T { End(0) }
}
module Second {
  import Third
  datatype U = U(v: Third.V) {
    invariant Third.Good(v)
    witness U(Third.Unit())
  }
  predicate Good(u: U) { Third.Good(u.v) }
}
module Third {
  import First
  datatype V = V(ts: seq<First.T>) {
    invariant forall t <- ts :: t.End? ==> t.n >= 0
    witness V([])
  }
  predicate Good(v: V) { forall t <- v.ts :: t.End? ==> t.n >= 0 }
  function Unit(): V { V([]) }
}

module Classes {
  import Checks
  class C {
    const n: int
    invariant Checks.Nonnegative(n)
    constructor () { n := 0; }
  }
  method Make() returns (c: C) { c := new C(); }
}
module Checks {
  import Classes
  predicate Nonnegative(n: int) { n >= 0 }
  lemma Use(c: Classes.C) { assert c.n >= 0; }
}
