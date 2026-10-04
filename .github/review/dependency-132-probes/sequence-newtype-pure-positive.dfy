class C {
  var x: int
  var next: C?
  constructor () { x := 0; next := null; }
  function F(): C reads this { this }
  twostate function P(): C? reads this { old(next) }
}
twostate lemma AllocatedNew<T(!new)>(new x: T) ensures old(allocated(x)) {}
newtype PureWrapped = values: seq<() -> int> | true witness []
datatype PureBox = PureBox(value: PureWrapped)
method Probe() {
  label L:
  var n := new C();
  var f := () => if n == n then 0 else 1;
  var values := [f] as PureWrapped;
  var b := PureBox(values);
  assert old@L(allocated(b));
  AllocatedNew@L(values);
}
