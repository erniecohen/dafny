datatype GenericCallbackResult<R> = GenericCallbackResult(value: R)

function CallbackValue<T>(value: T): int -> T {
  (n: int) => value
}

function CallbackFactory<R, U>(mapping: R -> U): (R, int) -> (int -> U) {
  (value: R, remaining: int) => CallbackValue(mapping(value))
}

function CallbackOfResult<R, U>(input: int -> GenericCallbackResult<R>, mapping: R -> U): int -> U {
  (n: int) => CallbackFactory(mapping)(input(n).value, 0)(0)
}

lemma GenericExtractedCallback<R, U>(input: int -> GenericCallbackResult<R>, mapping: R -> U, n: int) {
  var composite := CallbackOfResult(input, mapping);
  assert composite(n) == mapping(input(n).value);
  assert false;
}
