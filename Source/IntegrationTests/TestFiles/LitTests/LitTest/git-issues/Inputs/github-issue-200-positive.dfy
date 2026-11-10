// The program of https://github.com/erniecohen/dafny/issues/200, part 2, without the export (part 1).
// b is used only as Q's second argument, whose declared type is the subset type T.
module M {
  datatype D = D(s: iset<int>)
  type T(!new) = d: D | d.s != iset{} witness *
  ghost predicate Q(F: iset<T>, b: T) { forall x | x in F :: x.s <= b.s }
  ghost predicate P(F: iset<T>) requires exists b :: Q(F, b) { true }
  ghost predicate R(F: iset<T>) requires forall b :: Q(F, b) { true }
  lemma UseP(F: iset<T>, t: T) requires Q(F, t) ensures P(F) {}
}
