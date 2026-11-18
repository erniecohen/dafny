module Lib { const {:termination_closed} CertificateRequested: bool := true
  method Wrong() {
    assert false; // error
  }
}
