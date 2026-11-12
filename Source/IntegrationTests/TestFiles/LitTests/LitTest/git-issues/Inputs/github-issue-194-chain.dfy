datatype D = D(v: int) {
  predicate {:operator "<="} le(b: D) requires 0 <= v { v <= b.v }
}
lemma Chain(a: D, b: D, c: D)
  requires 0 <= a.v
{
  if a <= b <= c { assert a.le(c); }
}
