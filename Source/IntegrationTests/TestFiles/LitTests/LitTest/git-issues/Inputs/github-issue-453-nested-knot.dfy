// A knot through a nested datatype, whose recursion makes a larger instantiation at every step, which
// proves false under --legacy-stored-function-callbacks (https://github.com/erniecohen/dafny/issues/453).
// The stored value has type D<int>, and its function takes a Wrap<int>, which holds the object, only
// in D<Wrap<int>>.  The search of D<int>, which made D<Wrap<int>>, D<Wrap<Wrap<int>>> and so on, did
// not end.
datatype Wrap<A> = Wrap(c: C, a: A)
datatype D<!A> = Leaf(f: A -> bool) | Node(next: D<Wrap<A>>)

class C {
  const d: D<int>
  constructor()
    ensures d.Node? && d.next.Leaf? && forall w: Wrap<int> :: d.next.f(w) == Loop(w)
  {
    d := Node(Leaf((w: Wrap<int>) => Loop(w)));
  }
}

function Loop(w: Wrap<int>): bool {
  match w.c.d
  case Node(Leaf(g)) => !g(w)
  case _ => false
}

method Main() {
  var c := new C();
  var w := Wrap(c, 0);
  assert Loop(w) == !c.d.next.f(w);
  assert Loop(w) == !Loop(w);
  assert false;
  print Loop(w), "\n";
}
