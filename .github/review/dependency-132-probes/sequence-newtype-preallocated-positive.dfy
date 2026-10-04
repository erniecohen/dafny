class C {
  var x: int
  var next: C?
  constructor () { x := 0; next := null; }
  function F(): C reads this { this }
  twostate function P(): C? reads this { old(next) }
}
newtype Wrapped = values: seq<() ~> int> | true witness []
datatype NominalBox = NominalBox(value: Wrapped)
method Probe(c: C) {
  label L:
  var f: () ~> int := () reads c => 0;
  var b := NominalBox([f] as Wrapped);
  assert allocated(b);
  assert old@L(allocated(b));
}
