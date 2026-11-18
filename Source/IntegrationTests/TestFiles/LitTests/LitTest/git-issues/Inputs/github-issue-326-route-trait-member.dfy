// A trait member used as a function value by its own override: a naked function.
datatype Tree = Leaf | Node(kids: nat -> Tree)

trait Tr { function M(n: nat): Tree }
class C extends Tr {
  function M(n: nat): Tree { Node(this.M) }
}
lemma Bad(c: C) ensures false { assert c.M(0) == Node(c.M); assert c.M(0) < Node(c.M); }
