module {:extern} A {
  import B
  class {:extern} T { function Local(): int { 1 } }
  function F(): int { 1 }
}
module B { import A function G(): int { A.F() } }
abstract module Mid refines A { }
module {:extern} Final refines Mid { }
