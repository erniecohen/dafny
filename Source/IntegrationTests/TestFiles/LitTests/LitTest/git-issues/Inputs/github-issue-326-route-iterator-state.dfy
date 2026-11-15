// An iterator that yields a tree built from a function value reading its own state: that value is not
// a total arrow.
datatype Tree = Leaf | Node(kids: nat -> Tree)

iterator It() yields (t: Tree)
{
  var g := (n: nat) reads this => t;
  t := Node(g);
  yield;
}
