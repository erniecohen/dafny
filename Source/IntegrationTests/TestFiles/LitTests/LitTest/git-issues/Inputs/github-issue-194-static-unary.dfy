datatype D = D(v: int) {
  static function {:operator "-"} negate(x: D): D { D(-x.v) }
  function {:operator "-"} minus(x: D): D { D(v - x.v) }
}
lemma Check(x: D, y: D) {
  assert -x == D.negate(x);
  assert x - y == x.minus(y);
}
