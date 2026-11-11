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
module Generic {
  datatype Box<T> = Box(v: T) {
    function twice(): Box<T> { this + this }
    function {:operator "+"} plus(b: Box<T>): Box<T> { b }
  }
  function use<T>(x: Box<T>): Box<T> { x + x }
  export reveals Box, Box.plus, Box.twice, use
}
module Client {
  import M
  import Generic
  lemma generic() { assert (Generic.Box(1) + Generic.Box(2)).v == 2; }
  lemma check() { assert (M.D(1) + M.D(2)).n == 3; }
}
