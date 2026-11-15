module {:foo D(1) + D(2)} M {
  datatype D = D(n: int) {
    function {:operator "+"} plus(b: D): D { D(n + b.n) }
  }
}
