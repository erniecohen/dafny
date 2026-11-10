// b is passed both to P0, whose formal is a D, and to Q, whose formal is a T, so its type stays
// the base type D under every option.  The postcondition holds only over D (its witness
// D(iset{}) is not a T), and the assert false must fail.
module M {
  datatype D = D(s: iset<int>)
  type T(!new) = d: D | d.s != iset{} witness *
  ghost predicate Q(F: iset<T>, b: T) { forall x | x in F :: x.s <= b.s }
  ghost predicate P0(b: D) { b.s == iset{} }
  lemma NeedsD(F: iset<T>)
    ensures exists b :: P0(b) || Q(F, b)
  {
    assert P0(D(iset{}));
  }
  lemma Vacuity(F: iset<T>)
    requires exists b :: Q(F, b)
  {
    assert false; // error: the inferred domain adds no fact
  }
}
