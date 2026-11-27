module A {
  import B
  class C { method {:extern "Foreign", "Call"} M() }
  function F(): int { 1 }
}
module B { import A function G(): int { A.F() } }
