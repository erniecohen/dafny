module {:extern} A {
  import B
  class {:extern} C { var {:extern "Foreign", "number"} Number: int }
  function F(): int { 1 }
}
module B { import A function G(): int { A.F() } }
