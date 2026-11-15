// A class constant whose initializer uses a member function that returns it: a const cycle.
datatype Tree = Leaf | Node(kids: nat -> Tree)

class C {
  const t: Tree := Node(M)
  function M(n: nat): Tree { t }
}
lemma Bad(c: C) ensures false { assert c.t == Node(c.M); assert c.M(0) == c.t; assert c.M(0) < c.t; }
