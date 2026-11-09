// A subset-type invariant with an existential under a universal.
datatype Cut = Cut(below: iset<int>) {
  ghost predicate valid() {
    (exists q :: q in below) && forall q | q in below :: exists p :: p in below && q < p
  }
}
type C = c: Cut | c.valid() witness *

lemma Larger(x: C, q: int) returns (p: int)
  requires q in x.below
  ensures p in x.below && q < p
{ p :| p in x.below && q < p; }

lemma Unrelated(x: C, y: C) ensures x.below == y.below ==> x == y {}
