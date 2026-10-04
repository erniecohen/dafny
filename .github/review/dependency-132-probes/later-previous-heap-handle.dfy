class C {
  var x: int
  var next: C?
  constructor () { x := 0; next := null; }
  function F(): C reads this { this }
  twostate function P(): C? reads this { old(next) }
}
twostate lemma MakeHandle(c: C) returns (g: () ~> C?)
  ensures old(allocated(g))
{
  g := c.P;
}
method Probe(c: C) modifies c {
  label L:
  var n := new C();
  c.next := n;
  label K:
  ghost var g := MakeHandle@K(c);
  assert allocated(g);
  assert old@K(allocated(g));
  assert old@L(allocated(g));
}
