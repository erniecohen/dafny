class C {
  var value: int
  function {:operator "+"} plus(x: int): int reads this { value + x }
}
function Bad(c: C): int { c.plus(1) }
