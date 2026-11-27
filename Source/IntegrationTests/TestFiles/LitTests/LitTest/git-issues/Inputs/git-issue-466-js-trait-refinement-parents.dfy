module {:extern} A {
  import B
  trait Parent extends object { }
  trait {:extern} Child extends Parent { }
  function F(): int { 1 }
}
module B { import A function G(): int { A.F() } }
module {:extern} Refined refines A { }
