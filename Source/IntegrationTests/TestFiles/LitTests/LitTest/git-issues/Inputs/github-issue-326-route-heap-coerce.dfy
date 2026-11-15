// A heap-reading lambda coerced from ~> to ->: the subset constraint fails.
datatype Tree = Leaf | Node(kids: nat -> Tree)

class C { var t: Tree }
method Bad(c: C) modifies c ensures false {
  var f: nat ~> Tree := (n: nat) reads c => c.t;
  var g: nat -> Tree := f;
  c.t := Node(g);
  assert g(0) == Node(g);
}
