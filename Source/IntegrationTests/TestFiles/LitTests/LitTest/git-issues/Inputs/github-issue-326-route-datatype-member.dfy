// A datatype member function that returns a tree built from itself as a value: a naked function.
datatype Tree = Leaf | Node(kids: nat -> Tree)

datatype D = D {
  function M(n: nat): Tree { Node(this.M) }
}
lemma Bad(d: D) ensures false { assert d.M(0) == Node(d.M); assert d.M(0) < Node(d.M); }
