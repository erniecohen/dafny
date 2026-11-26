module {:extern} A {
  import B
  import Outside
  trait {:extern} Child extends Outside.Root { }
  function F(): int { 1 }
}
module B { import A function G(): int { A.F() } }
