class C {
  var x: int
  var next: C?
  constructor () { x := 0; next := null; }
  function F(): C reads this { this }
  twostate function P(): C? reads this { old(next) }
}
method Probe(c: C) {
  label L:
  var f := c.F;
  assert allocated(f);
  assert old@L(allocated(f));
}
