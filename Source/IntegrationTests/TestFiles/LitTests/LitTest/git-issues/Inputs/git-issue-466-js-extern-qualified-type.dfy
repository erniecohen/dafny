module {:extern} A {
  import B
  class {:extern "Foreign", "Actual"} T { }
  function F(): int { 1 }
}
module B { import A function G(): int { A.F() } }
