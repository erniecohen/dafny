// Control: an assume in the body of a least lemma still refuses the library.
module M {
  least predicate Even(x: nat) { x == 0 || (x >= 2 && Even(x - 2)) }
  greatest predicate Always(x: int) { Always(x) }
  least lemma EvenPlusTwo(x: nat) requires Even(x) ensures Even(x + 2) { assume false; }
  greatest lemma AlwaysHolds(x: int) ensures Always(x) { AlwaysHolds(x); }
}
