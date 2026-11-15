datatype D = D(v: int) {
  function {:operator "+"} plus(b: D): D { D(v + b.v) }
}
function f(x: D, y: D): D { BODY }
lemma check(x: D, y: D) ensures f(x, y).v == x.v + y.v { }
