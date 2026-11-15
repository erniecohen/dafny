datatype D = D(n: int) {
  opaque function {:operator "+"} plus(b: D): D { D(n + b.n) }
}
lemma use(x: D) {
  reveal x.plus();
  assert (x + x).n == 2 * x.n;
}
