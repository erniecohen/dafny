// Two class constants, a function value that returns the other and a tree built from it: a const cycle.
datatype Tree = Leaf | Node(kids: nat -> Tree)

class C {
  const f: nat -> Tree := (n: nat) => t
  const t: Tree := Node(f)
}
lemma Bad(c: C) ensures false { assert c.t == Node(c.f); assert c.f(0) == c.t; assert c.f(0) < c.t; }
