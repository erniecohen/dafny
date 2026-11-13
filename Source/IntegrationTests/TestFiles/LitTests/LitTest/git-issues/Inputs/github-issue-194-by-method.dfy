datatype D = D(n: int) {
  function {:operator "+"} plus(b: D): D { D(n + b.n) }
  by method { return D(n + b.n); }
}
method Main() { print (D(1) + D(2)).n, "\n"; }
