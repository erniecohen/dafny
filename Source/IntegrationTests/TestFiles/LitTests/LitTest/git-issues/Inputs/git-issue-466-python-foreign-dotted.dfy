module {:extern "Foreign.Nested"} A {
  import B
  function F(): int { B.Base() + 1 }
}
module B {
  import A
  function Base(): int { 10 }
  function Read(): int { A.F() }
}
method Main() { print B.Read(), "\n"; }
