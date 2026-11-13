datatype D = D(v: int) {
  function {:operator "+"} plus(b: D): D { D(v + b.v) }
}
lemma Check(x: D) {
  calc { x + x; == x.plus(x); }
}
