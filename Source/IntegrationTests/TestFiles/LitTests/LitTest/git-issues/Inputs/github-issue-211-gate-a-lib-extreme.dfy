// Gate (a) of https://github.com/erniecohen/dafny/issues/211: a library with a least lemma under a bound.
module Lib {
  // Open, with a lemma slot: an implementation proves its own claim.
  trait {:termination false} W {
    ghost predicate Claim(n: nat)
      decreases {:global} n, 0
    lemma P(n: nat)
      ensures Claim(n)
      decreases {:global} n, 1
  }

  least predicate {:termination_closed} Even(x: nat) { x == 0 || (x >= 2 && Even(x - 2)) }

  // By induction on the derivation of Even(x); it also gives the claim of w below n.
  least lemma Walk(w: W, n: nat, x: nat)
    requires Even(x)
    ensures x % 2 == 0
    ensures n > 0 ==> w.Claim(n - 1)
    decreases {:global} n, 1
  {
    if n > 0 {
      w.P(n - 1);
    }
    if x != 0 {
      Walk(w, n, x - 2);
    }
  }
}
