class C {
  var x: int
  var next: C?
  constructor () { x := 0; next := null; }
  function F(): C reads this { this }
  twostate function P(): C? reads this { old(next) }
}
newtype Wrapped = values: seq<() ~> int> | true witness []
method Probe() ensures false {
  label L:
  var n := new C();
  var f: () ~> int := () reads n => 0;
  var values := [f] as Wrapped;
  var produce := () => values;
  assert old@L(allocated(produce));
  assert old@L(allocated(produce()));
  assert old@L(n in ((produce() as seq<() ~> int>)[0]).reads());
  assert !old@L(allocated(n));
}
