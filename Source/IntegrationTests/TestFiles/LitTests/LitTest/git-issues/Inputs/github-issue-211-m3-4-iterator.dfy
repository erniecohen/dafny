module It { const {:termination_closed} CertificateRequested: bool := true
  iterator Gen() yields (x: int) {
    x := 1;
    yield;
  }
}
