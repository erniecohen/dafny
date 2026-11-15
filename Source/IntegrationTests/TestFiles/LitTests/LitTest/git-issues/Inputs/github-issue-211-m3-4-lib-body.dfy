// The library of github-issue-211-m3-4-lib.dfy with only a method body changed: same contracts.
module Lib {
  function Double(x: nat): nat { x + x }

  method Count(n: nat) returns (r: nat)
    ensures r == n
  {
    r := n;
  }

  lemma DoubleIsEven(x: nat)
    ensures Double(x) % 2 == 0
  {
  }
}
