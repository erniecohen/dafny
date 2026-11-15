module Library {
  trait {:termination false} Root {
    method F(n: nat) decreases {:global} n
    function G(n: nat): nat decreases {:global} n
  }
  trait {:termination false} Child extends Root {
    method F(n: nat)
    function G(n: nat): nat
  }
}
module Client {
  import Library
  class C extends Library.Child {
    method F(n: nat) decreases {:global} n + 1 { }
    function G(n: nat): nat decreases {:global} n + 1 { 0 }
  }
}
