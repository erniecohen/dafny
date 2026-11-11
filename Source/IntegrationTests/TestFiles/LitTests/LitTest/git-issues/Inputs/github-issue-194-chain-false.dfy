datatype D = D(v: int) {
  predicate {:operator "<="} le(b: D) { v != b.v }
}
method Main() {
  var a := D(1); var b := D(2);
  assert a <= b <= a;
  assert a <= a; // FALSE_CONTROL
}
