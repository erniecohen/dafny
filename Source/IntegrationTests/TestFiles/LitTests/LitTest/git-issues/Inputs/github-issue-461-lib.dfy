// A library with least and greatest predicates and lemmas, and no assumption: it builds under --modular-termination.
module M { const {:termination_closed} CertificateRequested: bool := true
  least predicate Even(x: nat) { x == 0 || (x >= 2 && Even(x - 2)) }
  greatest predicate Always(x: int) { Always(x) }
  least lemma EvenPlusTwo(x: nat) requires Even(x) ensures Even(x + 2) { }
  greatest lemma AlwaysHolds(x: int) ensures Always(x) { AlwaysHolds(x); }
}
