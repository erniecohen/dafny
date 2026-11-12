module M {
  datatype D = D(n: int) {
    function {:operator "+"} plus(b: D): D { D(n + b.n) }
  }
  function twice(x: D): D { x + x }
  export reveals D, twice
}
