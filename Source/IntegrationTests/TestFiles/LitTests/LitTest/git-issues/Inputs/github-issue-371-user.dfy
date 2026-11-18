module User { const {:termination_closed} CertificateRequested: bool := true
  opaque const K: nat := 5

  lemma Wrong()
    ensures K == 6
}
