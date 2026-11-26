module {:extern} A {
  import B
  trait {:extern} Parent { }
  trait {:extern} Child extends Parent { }
  function F(): int { 1 }
}
module B { import A function G(): int { A.F() } }
module {:extern} Refined refines A { }
