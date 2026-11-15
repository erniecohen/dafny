// The contract of Count is that of github-issue-211-m3-4-lib.dfy, but its body only assumes it.
module Lib {
  function Double(x: nat): nat { x + x }

  method Count(n: nat) returns (r: nat)
    ensures r == n
  {
    assume {:axiom} false;
    r := 0;
    while true { }
  }

  lemma DoubleIsEven(x: nat)
    ensures Double(x) % 2 == 0
  {
  }
}
