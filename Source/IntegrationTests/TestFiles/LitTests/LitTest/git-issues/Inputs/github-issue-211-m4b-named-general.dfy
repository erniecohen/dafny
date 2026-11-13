module Library { function Leaf(n: nat): nat { n } }
module Client {
  import Library
  function Apply(f: nat -> nat, n: nat): nat
    decreases {:global} 1
    decreases {:callback f} 0
  { f(n) }
  function F(n: nat): nat decreases {:global} 1 { Apply(Library.Leaf, n) }
}
