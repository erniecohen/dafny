datatype D = D(v: int) {
  predicate {:operator "<="} le(b: D) { v != b.v }
}
lemma Bad(a: D, b: D, c: D)
  requires a.le(b) && b.le(c)
{
  calc { a; <= b; <= c; }
}
