class FreshReference82 {}

datatype FreshCallbackResult82<R> = FreshCallbackResult82(value: R)

function FreshCallbackValue82<T>(value: T): int -> T {
  (n: int) => value
}

function FreshCallbackFactory82<R, U>(mapping: R -> U): (R, int) -> (int -> U) {
  (value: R, remaining: int) => FreshCallbackValue82(mapping(value))
}

function FreshComposite82<R, U>(input: int -> FreshCallbackResult82<R>, mapping: R -> U): int -> U {
  (n: int) => FreshCallbackFactory82(mapping)(input(n).value, 0)(0)
}

lemma FreshGenericComposite82<R, U>(input: int -> FreshCallbackResult82<R>, mapping: R -> U, n: int)
  ensures FreshComposite82(input, mapping)(n) == mapping(input(n).value)
{
  var composite := FreshComposite82(input, mapping);
  assert composite(n) == mapping(input(n).value);
}

method FreshReferenceCurrentOld82() {
  label Before:
  var c := new FreshReference82;
  ghost var input := (n: int) => FreshCallbackResult82(c);
  ghost var mapping := (r: FreshReference82) => r;
  ghost var composite := FreshComposite82(input, mapping);
  FreshGenericComposite82(input, mapping, 0);
  assert input(0) == FreshCallbackResult82(c);
  assert mapping(c) == c;
  assert composite(0) == c;
  assert !old@Before(allocated(c));
  assert false;
}
