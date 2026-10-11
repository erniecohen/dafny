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

// The original program and the one above, with the function value carried by a container or a datatype,
// whose allocation is that of its members.
datatype Box = Box(g: () ~> int)
datatype RBox = RBox(g: () -> C)

method ThroughSeq() ensures false {
  label L:
  var n := new C();
  var s := [() => n];
  assert old@L(allocated(s));
  assert old@L(allocated(s[0]()));
  assert !old@L(allocated(n));
}

method ThroughMap() ensures false {
  label L:
  var n := new C();
  var m := map[0 := () => n];
  assert old@L(allocated(m));
  assert old@L(allocated(m[0]()));
  assert !old@L(allocated(n));
}

method ThroughISet(c: C) requires c.x != 5 modifies c ensures false {
  label L:
  c.x := 5;
  label K:
  var n := new C();
  var f := () reads c, (if c.x == 5 then {n} else {}) => 0;
  ghost var s := iset{f};
  assert old@L(allocated(s));
  assert old@K(allocated(s)) ==> old@K(allocated(f));
  assert !old@K(allocated(n));
  assert old@K(n in f.reads());
}

method ThroughDatatype() ensures false {
  label L:
  var n := new C();
  var b := RBox(() => n);
  assert old@L(allocated(b));
  assert old@L(allocated(b.g()));
  assert !old@L(allocated(n));
}

// A general arrow whose signature mentions no reference type still captures references, and its reads
// frame shows them: neither a datatype holding it nor a lambda capturing it is allocated before them.
method ReadsThroughDatatype() ensures false {
  label L:
  var n := new C();
  var g := () reads n => 0;
  var b := Box(g);
  assert old@L(allocated(b));
  assert old@L(n in g.reads());
  assert !old@L(allocated(n));
}

method ReadsThroughCapture() ensures false {
  label L:
  var n := new C();
  var g := () reads n => 0;
  var f := () => g;
  assert old@L(allocated(f));
  assert old@L(allocated(f()));
  assert old@L(n in f().reads());
  assert !old@L(allocated(n));
}
