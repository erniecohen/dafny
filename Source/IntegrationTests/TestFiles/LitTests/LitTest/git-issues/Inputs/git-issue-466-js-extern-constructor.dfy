module {:extern} A {
  import B
  class {:extern} T { constructor {:extern} (n: int) }
  function F(): int { 1 }
}
module B { import A function G(): int { A.F() } }
