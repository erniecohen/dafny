module A {
  import B
  method {:extern} Foreign()
  function F(): int { 1 }
}
module B { import A function G(): int { A.F() } }
