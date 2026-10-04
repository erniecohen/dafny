class C {
  var x: int
  var next: C?
  constructor () { x := 0; next := null; }
  function F(): C reads this { this }
  twostate function P(): C? reads this { old(next) }
}
newtype Wrapped = values: seq<() ~> int> | true witness []
datatype NominalBox = NominalBox(value: Wrapped)
method Probe() ensures false {
  label L:
  var n := new C();
  var f: () ~> int := () reads n => 0;
  var b := NominalBox([f] as Wrapped);
  assert old@L(allocated(b));
  var values := b.value as seq<() ~> int>;
  assert old@L(allocated(values[0]));
  assert old@L(n in values[0].reads());
  assert !old@L(allocated(n));
}
