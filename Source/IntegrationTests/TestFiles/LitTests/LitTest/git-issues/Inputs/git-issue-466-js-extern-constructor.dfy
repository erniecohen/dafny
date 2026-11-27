module {:extern} A {
  import B
  class {:extern} T { constructor {:extern} (n: int) function {:extern} Local(): int { 1 } }
  function F(): int { 1 }
}
module B { import A function G(): int { A.F() } }
