newtype WrappedMapKeys = m: map<map<bool, int>, bool> | true

ghost function UnboundedWrappedMapKeys(): WrappedMapKeys {
  map i: int | true :: map[true := i] := true
}
