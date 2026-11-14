// Dynamic dispatch through a trait within one module: the override's termination check fails (the
// definition is then inconsistent with the rank facts, as in github-issue-326-route-eta.dfy).
datatype Tree = Leaf | Node(kids: nat -> Tree)

trait Tr { function Get(): nat -> Tree }
function Use(t: Tr): Tree { Node(t.Get()) }
class C extends Tr {
  function Get(): nat -> Tree { (n: nat) => Use(this) }
}
lemma Bad(c: C) ensures false { assert Use(c) == Node(c.Get()); assert c.Get()(0) == Use(c); assert c.Get()(0) < Node(c.Get()); }
