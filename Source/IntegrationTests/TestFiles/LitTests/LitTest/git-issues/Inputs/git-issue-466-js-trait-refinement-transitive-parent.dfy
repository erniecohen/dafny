module {:extern} ForeignBase {
  import ForeignPeer
  trait {:extern} Parent extends object { function Local(): int { 1 } }
}
module ForeignPeer { import ForeignBase }
module {:extern} A {
  import B
  import ForeignBase
  trait {:extern} Middle extends ForeignBase.Parent { }
  trait {:extern} Child extends Middle { }
  function F(): int { 1 }
}
module B { import A function G(): int { A.F() } }
module {:extern} Refined refines A { }
