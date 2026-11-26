module {:extern} A {
  import B
  trait {:extern} T { function {:extern} Local(): int { 1 } }
  function F(): int { 1 }
}
module B { import A function G(): int { A.F() } }
module {:extern} Refined refines A { }
