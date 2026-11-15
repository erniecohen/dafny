abstract module A { }
module B refines A {
  datatype D = D(n: int) {
    function {:operator "+"} plus(b: D): D { D(n + b.n) }
  }
}
