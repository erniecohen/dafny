// An iterator whose yield parameter would be a function value returning a tree built from itself: the
// lambda reads the iterator's state without a reads clause.
datatype Tree = Leaf | Node(kids: nat -> Tree)

iterator It() yields (f: nat -> Tree)
{
  f := (n: nat) => Node(f);
  yield;
}
