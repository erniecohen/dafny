// A datatype constant member built from a member function that returns it: a const cycle.
datatype Tree = Leaf | Node(kids: nat -> Tree)

datatype D = D {
  const t: Tree := Node(M)
  function M(n: nat): Tree { t }
}
lemma Bad(d: D) ensures false { assert d.t == Node(d.M); assert d.M(0) == d.t; assert d.M(0) < d.t; }
