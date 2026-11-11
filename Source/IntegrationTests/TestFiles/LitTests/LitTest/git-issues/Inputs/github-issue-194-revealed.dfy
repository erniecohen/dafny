module M {
  datatype D = D(n: int) {
    function {:operator "+"} plus(b: D): D { D(n + b.n) }
    predicate {:operator "<="} le(b: D) { n <= b.n }
  }
  // The selected function occurs before its declaration is cloned.
  function twice(x: D): D { x + x }
  lemma check(x: D) ensures (x + x).n == 2 * x.n { }
  export reveals D, D.plus, D.le, twice, check
}
module Client {
  import M
  lemma check() { assert (M.D(1) + M.D(2)).n == 3; }
}
