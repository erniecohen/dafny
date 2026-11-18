module Lib { const {:termination_closed} CertificateRequested: bool := true
  method {:extern "Lib", "Count"} Count(n: nat) returns (r: nat)
    ensures r == n
}
