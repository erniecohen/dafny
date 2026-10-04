class C {
  var x: int
  var next: C?
  constructor () { x := 0; next := null; }
  function F(): C reads this { this }
  twostate function P(): C? reads this { old(next) }
}
method Probe() ensures false {
  label L:
  var n := new C();
  var g: () ~> int := () reads n => 0;
  var f := () => g;
  assert old@L(allocated(f));
  assert old@L(allocated(f()));
  assert old@L(n in f().reads());
  assert !old@L(allocated(n));
}
