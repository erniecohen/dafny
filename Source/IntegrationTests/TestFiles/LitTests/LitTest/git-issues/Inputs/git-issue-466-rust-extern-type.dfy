module A {
  import B
  class {:extern "Foreign", "Actual"} Foreign {
    function Local(): int { B.G() }
  }
  function F(): int { 1 }
}
module B { import A function G(): int { A.F() } }
