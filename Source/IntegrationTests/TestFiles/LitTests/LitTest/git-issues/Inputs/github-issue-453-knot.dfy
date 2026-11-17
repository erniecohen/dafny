// The reproducer of https://github.com/erniecohen/dafny/issues/453, which proves false under
// --legacy-stored-function-callbacks.
datatype Box = Box(c: C)

class C {
  const g: Box -> nat
  constructor()
    ensures forall b: Box :: g(b) == b.c.F(0)
  {
    g := (b: Box) => b.c.F(0);
  }
  function F(n: nat): nat { g(Box(this)) + 1 }
}

method Main() {
  var c := new C();
  assert c.F(0) == c.g(Box(c)) + 1;
  assert false;
  print c.F(0), "\n";
}
