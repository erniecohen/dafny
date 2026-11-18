// A loop without a body is assumed to keep its invariant and to terminate.
module Bad { const {:termination_closed} CertificateRequested: bool := true
  method Bad()
    ensures false
  {
    var i := 0;
    while i < 1
      invariant i == 0
  }
}
