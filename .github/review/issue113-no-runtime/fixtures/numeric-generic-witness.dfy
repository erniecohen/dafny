// Existing-language phantom generic numeric newtype with a compiled witness.
newtype Counter<T(0)> = x: int | true witness 0

method Observe<T(0)>() returns (value: int) {
  var n: Counter<T>;
  value := n as int;
}

method Main() {
  var a := Observe<int>();
  var b := Observe<bool>();
  expect a == 0 && b == 0;
  print a, " ", b, "\n";
}
