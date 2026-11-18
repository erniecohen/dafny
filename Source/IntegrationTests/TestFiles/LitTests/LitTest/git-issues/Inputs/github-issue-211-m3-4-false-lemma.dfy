// Verifies only under an inconsistent prelude.
module Bad { const {:termination_closed} CertificateRequested: bool := true
  lemma Bad()
    ensures false
  {
  }
}
