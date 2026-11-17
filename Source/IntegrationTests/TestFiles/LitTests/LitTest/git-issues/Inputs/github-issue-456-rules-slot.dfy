// https://github.com/erniecohen/dafny/issues/456: a least lemma as the slot of an open trait, and a generic least lemma.
// Every member Ann must be rejected, and the implementation of LL may not raise the bound of its slot.
module Lib {
  trait {:termination false} T {
    lemma L(n: nat)
      ensures false
      decreases {:global} n
    least lemma LL(n: nat)
      ensures false
      decreases {:global} n
  }
}
module Client {
  import opened Lib
  least lemma A14<X>(t: T, s: seq<X>, x: X) ensures false decreases {:global} |s|
  { if * { A14(t, s + [x], x); } else if |s| > 0 { t.L(|s| - 1); } else { assume false; } }
  least lemma A17(t: T, n: nat) ensures false decreases {:global} n { t.LL(n); }
  lemma A18(t: T, n: nat, k: ORDINAL) ensures false decreases {:global} n { t.LL#[k](n); }
  class C extends T {
    lemma L(n: nat) ensures false { A14<int>(this, [], 0); }
    least lemma LL(n: nat) ensures false decreases {:global} n + 1 { }
  }
}
