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
method Probe(c: C) {
  label K:
  var f := c.F;
  ghost var g := MakeHandle@K(c);
  assert allocated(f) && allocated(g);
  assert old@K(allocated(f)) && old@K(allocated(g));
  assert false;
}
