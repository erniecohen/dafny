module Library {
  function Leaf(n: nat): nat { n }
}
module Client {
  import Library
  function F(n: nat): nat decreases {:global} Library.Leaf(n) { n }
}
