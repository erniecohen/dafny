newtype WrappedMap<K(!new), V(!new)> = m: map<K, V> | true
newtype NestedMap<K(!new), V(!new)> = m: WrappedMap<K, V> | true

ghost function WrappedCapturedMap(n: int): NestedMap<int, int> {
  map i: int | i == n :: 0 := i
}

lemma WrappedMapsRemainLive(n: int) {
  var first := WrappedCapturedMap(n) as map<int, int>;
  var next := WrappedCapturedMap(n + 1) as map<int, int>;
  assert first.Keys == {0};
  assert next.Keys == {0};
  assert first[0] == n;
  assert next[0] == n + 1;
  assert false;
}
