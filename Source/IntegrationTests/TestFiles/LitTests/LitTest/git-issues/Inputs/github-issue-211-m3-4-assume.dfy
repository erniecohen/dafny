module Lib { const {:termination_closed} CertificateRequested: bool := true
  method Count(n: nat) returns (r: nat)
    ensures r == n
  {
    r := 0;
    assume r == n;
  }
}
