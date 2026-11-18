module Lib { const {:termination_closed} CertificateRequested: bool := true
  method Spin(n: nat)
    decreases *
  {
    Spin(n);
  }
}
