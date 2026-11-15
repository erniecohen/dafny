module A {
  function F(x: int): int { x + 1 }
  function Value(): int -> int { F }
}
