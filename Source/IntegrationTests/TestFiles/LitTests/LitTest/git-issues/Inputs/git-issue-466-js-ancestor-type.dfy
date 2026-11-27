module {:extern} ForeignBase {
  trait {:extern "Foreign", "Root"} Parent extends object {  }
}
module {:extern} A {
  import B
  import ForeignBase
  trait {:extern} Child extends ForeignBase.Parent { }
  function F(): int { 1 }
}
module B { import A function G(): int { A.F() } }
module {:extern} Refined refines A { }
