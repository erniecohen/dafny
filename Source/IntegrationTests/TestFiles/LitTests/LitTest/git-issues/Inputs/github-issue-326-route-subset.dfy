// A subset type over ~> with the total-arrow constraint written out: it does not reach the built-in
// total arrow, so its field gets no rank fact (the recursion fails), and a heap-reading lambda does
// not meet its constraint.

type Tot<T> = f: nat ~> T | forall n :: f.reads(n) == {} && f.requires(n) witness *
datatype U = ULeaf | UNode(kids: Tot<U>)
class C { var u: U }
method Bad(c: C) modifies c ensures false {
  var f := (n: nat) reads c => c.u;
  var g: Tot<U> := f;
  c.u := UNode(g);
  assert g(0) == UNode(g);
}
function USize(u: U): nat {
  if u.ULeaf? then 0 else USize(u.kids(0)) + 1
}
