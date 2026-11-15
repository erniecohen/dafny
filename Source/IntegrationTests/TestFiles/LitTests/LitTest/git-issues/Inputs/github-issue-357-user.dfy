abstract module Base {
  opaque function Double(n: nat): nat { n + n }
}

module Leaf refines Base {
  lemma {:verify false} Wrong()
    ensures Double(1) == 3
  {
  }
}
