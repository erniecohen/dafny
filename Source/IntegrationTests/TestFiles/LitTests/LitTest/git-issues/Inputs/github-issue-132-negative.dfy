class C {
  var x: int
  constructor () {}
}

// f reads nothing, so its reads frame is allocated everywhere, but it returns n.
method NoHeapSuccession() ensures false {
  label L:
  var n := new C();
  var f := () => n;
  assert old@L(allocated(f));
  assert old@L(allocated(f()));
  assert !old@L(allocated(n));
}

// A function value is not allocated before what it captures.
method CapturedLater() {
  label L:
  var n := new C();
  var f := () reads n => n.x;
  assert old@L(allocated(f));
}

method CapturedLaterNested() {
  label L:
  var n := new C();
  var g := () => n;
  var f := () => g;
  assert old@L(allocated(f));
}

// Vacuity controls, with the new allocation facts in play.
method Vacuity(c: C) modifies c {
  label L:
  c.x := c.x + 1;
  var f := (k: int) reads c => c.x + k;
  var g := () reads c => old(c.x);
  var h := () reads c => old@L(c.x);
  assert allocated(f) && allocated(g) && allocated(h);
  assert old@L(allocated(f)) && old@L(allocated(h));
  assert false;
}

method VacuityAfterAllocation(c: C) modifies c {
  c.x := c.x + 1;
  var n := new C();
  var f := () reads c, n => c.x + n.x;
  assert allocated(f);
  assert old(allocated(f));
}
