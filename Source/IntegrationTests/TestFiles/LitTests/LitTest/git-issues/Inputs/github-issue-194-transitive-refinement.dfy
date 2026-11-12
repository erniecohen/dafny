module Numbers {
  datatype D = D(n: int) {
    function {:operator "+"} plus(b: D): D { D(n + b.n) }
  }
}
abstract module A { import Numbers }
module B refines A { }
