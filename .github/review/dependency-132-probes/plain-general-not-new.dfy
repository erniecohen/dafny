class C {
  var x: int
  var next: C?
  constructor () { x := 0; next := null; }
  function F(): C reads this { this }
  twostate function P(): C? reads this { old(next) }
}
twostate lemma AllocatedNew<T(!new)>(new x: T) ensures old(allocated(x)) {}
method Probe() ensures false {
  label L:
  var n := new C();
  var f: () ~> int := () reads n => 0;
  AllocatedNew@L(f);
  assert old@L(n in f.reads());
  assert !old@L(allocated(n));
}
