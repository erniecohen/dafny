// b is also used as b.s, so its type stays the base type D under every option.
// The lemma's postcondition holds only over D (the witness D(iset{}) is not a T),
// and the assert false must fail.
module M {
  datatype D = D(s: iset<int>)
  type T(!new) = d: D | d.s != iset{} witness *
  ghost predicate Q(F: iset<T>, b: T) { forall x | x in F :: x.s <= b.s }
  lemma NeedsD(F: iset<T>)
    ensures exists b :: b.s == iset{} || Q(F, b)
  {
    var w := D(iset{});
    assert w.s == iset{};
  }
  lemma Vacuity(F: iset<T>)
    requires exists b :: Q(F, b)
  {
    assert false; // error: the inferred domain adds no fact
  }
}
