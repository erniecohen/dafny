// A library verified under --modular-termination.
module Lib { const {:termination_closed} CertificateRequested: bool := true
  function Double(x: nat): nat { x + x }

  method Count(n: nat) returns (r: nat)
    ensures r == n
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
