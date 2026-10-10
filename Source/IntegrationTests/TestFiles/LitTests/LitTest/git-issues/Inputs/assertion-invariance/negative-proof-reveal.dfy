opaque predicate P(i: int) { i >= 0 }
lemma L() { assert true by { reveal P(); } assert P(0); }
