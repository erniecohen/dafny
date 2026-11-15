function ReturnedPureValue<T>(value: T): int -> T {
  (n: int) => value
}

function ReturnedPureCallback<R, U>(mapping: R -> U): (R, int) -> (int -> U) {
  (value: R, remaining: int) => ReturnedPureValue(mapping(value))
}

lemma ReturnedGenericCallback<R, U>(mapping: R -> U, value: R) {
  var callback := ReturnedPureCallback(mapping);
  assert callback(value, 0).reads(0) == {};
  assert callback(value, 0)(0) == mapping(value);
  assert false;
}
