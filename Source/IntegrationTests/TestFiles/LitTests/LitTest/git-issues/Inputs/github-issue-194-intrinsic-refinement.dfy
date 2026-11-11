abstract module A {
  function f(n: int): int { n + 1 }
}
module B refines A { }
