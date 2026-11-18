// A forall statement without a body is assumed, not proved.
module Bad { const {:termination_closed} CertificateRequested: bool := true
  lemma Bad()
    ensures false
  {
    forall x: int
      ensures false
  }
}
