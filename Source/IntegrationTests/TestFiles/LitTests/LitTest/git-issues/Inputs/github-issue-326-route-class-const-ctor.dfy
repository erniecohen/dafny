// A class constant set in the constructor from a member function that returns it: 'this' is not
// available in the first division of the constructor.
datatype Tree = Leaf | Node(kids: nat -> Tree)

class C {
  const t: Tree
  constructor () { t := Node(M); }
  function M(n: nat): Tree { t }
}
lemma Bad(c: C) ensures false { assert c.t == Node(c.M); assert c.M(0) == c.t; assert c.M(0) < c.t; }
