// The open-set half of the invariant behind an opaque predicate, with one
// lemma that reveals it and returns the witness.
opaque ghost predicate Open(s: iset<int>) {
  forall q | q in s :: exists p :: p in s && q < p
}
datatype Cut = Cut(below: iset<int>) {
  ghost predicate valid() {
    (exists q :: q in below) && Open(below)
  }
}
type C = c: Cut | c.valid() witness *

lemma Larger(x: C, q: int) returns (p: int)
  requires q in x.below
  ensures p in x.below && q < p
{ reveal Open(); p :| p in x.below && q < p; }

lemma Unrelated(x: C, y: C) ensures x.below == y.below ==> x == y {}
