module {:extern} A {
  import B
  trait {:extern} T { }
  function F(): int { 1 }
}
module B { import A function G(): int { A.F() } }
module Refined refines A { }
