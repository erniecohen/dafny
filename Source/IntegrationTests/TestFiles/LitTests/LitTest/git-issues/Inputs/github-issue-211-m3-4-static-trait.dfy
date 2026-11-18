// A static member of a trait has no implementations, so a bodyless one is an assumption.
module Bad { const {:termination_closed} CertificateRequested: bool := true
  trait T {
    static lemma L()
      ensures false
  }

  lemma Bad()
    ensures false
  {
    T.L();
  }
}
