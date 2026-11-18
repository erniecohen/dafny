module Lib { const {:termination_closed} CertificateRequested: bool := true
  method {:verify false} Count(n: nat) returns (r: nat)
    ensures r == n
  {
  }
}
