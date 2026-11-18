// Control: {:axiom} written on a least lemma is still an unchecked premise, and refuses the library.
module M { const {:termination_closed} CertificateRequested: bool := true
  least predicate Even(x: nat) { x == 0 || (x >= 2 && Even(x - 2)) }
  greatest predicate Always(x: int) { Always(x) }
  least lemma {:axiom} EvenPlusTwo(x: nat) requires Even(x) ensures Even(x + 2)
  greatest lemma AlwaysHolds(x: int) ensures Always(x) { AlwaysHolds(x); }
}
