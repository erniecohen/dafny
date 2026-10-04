class C {
  var x: int
  var next: C?
  constructor () { x := 0; next := null; }
  function F(): C reads this { this }
  twostate function P(): C? reads this { old(next) }
}
datatype CallableBox = CallableBox(value: () ~> int)
method Probe() ensures false {
  label L:
  var n := new C();
  var f: () ~> int := () reads n => 0;
  var b := CallableBox(f);
  assert old@L(allocated(b));
  assert old@L(n in b.value.reads());
  assert !old@L(allocated(n));
}
