module User {
  opaque const K: nat := 5

  lemma Wrong()
    ensures K == 6
}
