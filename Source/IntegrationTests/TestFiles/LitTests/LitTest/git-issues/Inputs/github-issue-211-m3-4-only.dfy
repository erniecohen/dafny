module Lib { const {:termination_closed} CertificateRequested: bool := true
  method {:only} Count(n: nat) returns (r: nat)
    ensures r == n
  {
    r := n;
  }

  method Wrong() {
    assert false;
  }
}
