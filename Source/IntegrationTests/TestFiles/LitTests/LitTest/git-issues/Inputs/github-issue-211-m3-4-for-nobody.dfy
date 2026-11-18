// A for loop without a body is assumed to keep its invariant.
module Bad { const {:termination_closed} CertificateRequested: bool := true
  method Bad()
    ensures false
  {
    for i := 0 to 2
      invariant i == 0
  }
}
