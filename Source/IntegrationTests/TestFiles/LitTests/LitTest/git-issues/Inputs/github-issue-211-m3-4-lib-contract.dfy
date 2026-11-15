// The library of github-issue-211-m3-4-lib.dfy with a changed public contract: Count gains a decreases clause.
module Lib {
  function Double(x: nat): nat { x + x }

  method Count(n: nat) returns (r: nat)
    ensures r == n
    decreases n
  {
    r := 0;
    while r < n
      invariant r <= n
    {
      r := r + 1;
    }
  }

  lemma DoubleIsEven(x: nat)
    ensures Double(x) % 2 == 0
  {
  }
}
