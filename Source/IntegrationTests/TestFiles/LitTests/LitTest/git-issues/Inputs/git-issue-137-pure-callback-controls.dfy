// Regression: pure callbacks preserve allocation only where their captures are available.
class PureCallbackReference137 {}
datatype PureCallbackResult137<T> = PureCallbackResult137(value: T)

function Value137<T>(value: T): int -> T {
  (n: int) => value
}
function Factory137<R, U>(mapping: R -> U): (R, int) -> (int -> U) {
  (value: R, remaining: int) => Value137(mapping(value))
}
function Composite137<R, U>(input: int -> PureCallbackResult137<R>, mapping: R -> U): int -> U {
  (n: int) => Factory137(mapping)(input(n).value, 0)(0)
}
lemma Composition137<R, U>(input: int -> PureCallbackResult137<R>, mapping: R -> U, n: int)
  ensures Composite137(input, mapping)(n) == mapping(input(n).value)
{
  var composite := Composite137(input, mapping);
  assert composite(n) == mapping(input(n).value);
}
method Current137() {
  label Before:
  var c := new PureCallbackReference137;
  ghost var input := (n: int) => PureCallbackResult137(c);
  ghost var mapping := (r: PureCallbackReference137) => r;
  ghost var composite := Composite137(input, mapping);
  Composition137(input, mapping, 0);
  assert composite(0) == c;
  assert !old@Before(allocated(c));
}
method OldCapture137() {
  label Before:
  var c := new PureCallbackReference137;
  ghost var input := (n: int) => PureCallbackResult137(c);
  ghost var mapping := (r: PureCallbackReference137) => r;
  ghost var composite := Composite137(input, mapping);
  Composition137(input, mapping, 0);
  assert composite(0) == c;
  assert old@Before(allocated(composite)); // must fail: captures a fresh reference
}
method InhabitedFalse137() {
  var c := new PureCallbackReference137;
  ghost var input := (n: int) => PureCallbackResult137(c);
  ghost var mapping := (r: PureCallbackReference137) => r;
  ghost var composite := Composite137(input, mapping);
  Composition137(input, mapping, 0);
  assert composite(0) == c;
  assert false; // must be the only failure in this method
}
