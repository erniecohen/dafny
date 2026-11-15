datatype D = D(n: int) {
  function {:operator "+"} plus(b: D): D { this.plus(b) }
}
