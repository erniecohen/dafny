// A ~> lambda that reads the heap without saying so: its reads check fails.
datatype Tree = Leaf | Node(kids: nat -> Tree)

class C { var t: Tree }
method Bad(c: C) modifies c ensures false {
  var f: nat ~> Tree := (n: nat) => c.t;
  var g: nat -> Tree := f;
  c.t := Node(g);
  assert g(0) == Node(g);
}
