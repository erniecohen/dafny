abstract module Base { const {:termination_closed} CertificateRequested: bool := true
  opaque function Double(n: nat): nat { n + n }
}

module Leaf refines Base {
  lemma {:verify false} Wrong()
    ensures Double(1) == 3
  {
  }
}
