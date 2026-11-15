trait Named {
  function value(): int
}
class C extends Named {
  function value(): int { 1 }
  function {:operator "+"} plus(n: int): int { value() + n }
}
method Main() {
  var c := new C;
  assert c + 2 == c.plus(2);
}
