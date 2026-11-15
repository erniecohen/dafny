module {:foo F(0)} M {
  newtype A = x: B | true
  newtype B = x: A | true
  function F(x: A): int { 0 }
}
