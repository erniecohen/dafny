// NONUNIFORM: Rust-specific tests
// RUN: %baredafny run --target=rs --enforce-determinism --type-system-refresh --general-traits=full "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

trait Super<K, T> {
  function Get(): T
}

datatype KeysValue<K(==),V> extends Super<K, V> = KeysValue(keys: set<K>, value: V) {
  function Get(): V { value }
}

method Main() {
  expect KeysValue({1}, 3).Get() == 3;
  expect (KeysValue({1}, 5) as Super<int, int>).Get() == 5;
  print "ok";
}