module A {
  datatype D = D(n: int) {
    function {:operator "+"} plus(b: D): D { D(n + b.n) }
  }
}
abstract module B {
  import X : A
}
