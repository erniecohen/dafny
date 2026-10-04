class C {
  var x: int
  var next: C?
  constructor () { x := 0; next := null; }
  function F(): C reads this { this }
  twostate function P(): C? reads this { old(next) }
}
newtype Wrapped = values: seq<() ~> int> | true witness []
twostate lemma AllocatedNew<T(!new)>(new x: T) ensures old(allocated(x)) {}
method Probe() ensures false {
  label L:
  var n := new C();
  var f: () ~> int := () reads n => 0;
  var values := [f] as Wrapped;
  AllocatedNew@L(values);
  assert old@L(allocated(f));
  assert old@L(n in f.reads());
  assert !old@L(allocated(n));
}
