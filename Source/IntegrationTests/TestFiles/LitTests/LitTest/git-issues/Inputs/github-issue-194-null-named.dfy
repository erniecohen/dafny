class C {
  function {:operator "+"} plus(x: int): int { x }
}
function Bad(c: C?): int { c.plus(1) }
