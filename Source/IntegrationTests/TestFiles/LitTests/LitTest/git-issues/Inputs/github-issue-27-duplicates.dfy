module DuplicatePredicate {
  least predicate P() { true }
  predicate P() { true }
}
module DuplicateLemma {
  least lemma Lemma() {}
  lemma Lemma() {}
}
