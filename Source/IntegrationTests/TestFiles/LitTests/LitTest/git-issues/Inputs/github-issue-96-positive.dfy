// Proved only with --statement-expression-conclusions (github-issue-96.dfy).
ghost predicate P(x: int)
lemma LP(x: int) ensures P(x)

// The issue's program.
lemma Issue() {
  assert (forall n: nat :: assume false; false);
}

// The conclusion of an assert-by, under a quantifier.
lemma AssertBy() {
  assert forall x: int :: (assert P(x) by { LP(x); } P(x));
}

// Under a guard.
lemma Guarded() {
  assert forall x: int :: x > 0 ==> (assert P(x) by { LP(x); } P(x));
}

// At negative polarity the conclusion is conjoined.
lemma Negative() {
  assert !(exists x: int :: (assert P(x) by { LP(x); } !P(x)));
}
