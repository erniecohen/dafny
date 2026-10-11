class C {
  var x: int
  constructor () {}
  method Set5() modifies this ensures x == 5 { x := 5; }
}

// The reads frame of f is {c} at L and holds n at K, which is allocated after K.
method M(c: C) requires c.x != 5 modifies c ensures false {
  label L:
  c.Set5();
  label K:
  var n := new C();
  var f := () reads c, (if c.x == 5 then {n} else {}) => 0;
  assert old@L(allocated(f));
  assert !old@K(allocated(n));
  assert old@K(n in f.reads());
}
