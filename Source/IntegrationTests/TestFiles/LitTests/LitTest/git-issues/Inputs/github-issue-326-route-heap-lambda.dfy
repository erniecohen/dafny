// A lambda that reads the heap, stored where it reads: it is not a total arrow.
datatype Tree = Leaf | Node(kids: nat -> Tree)

class C { var t: Tree }
method Bad(c: C) modifies c ensures false {
  var f := (n: nat) reads c => c.t;
  c.t := Node(f);
  assert f(0) == Node(f);
}
