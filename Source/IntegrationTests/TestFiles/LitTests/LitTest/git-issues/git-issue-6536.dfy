// RUN: %verify "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// The axiom for (o - m) + n applies exactly where o - m is defined, m <= o.Offset.
// The former guard n <= o.Offset + m also applied outside that domain.
// github-issue-23-ordinal-undefined.dfy checks that the invalid constant expression's
// postcondition is refused independently of its well-formedness error. Constant
// ordinal arithmetic can reach the solver; it is not all folded away.
// These two lemmas check the defined cases, including n > o.Offset + m, where the
// corrected axiom supplies a fact that the old guard excluded.

lemma MinusThenPlus(o: ORDINAL, m: nat, n: nat)
  requires m <= o.Offset && o.Offset + m < n
  ensures (o - (m as ORDINAL)) + (n as ORDINAL) == o + ((n - m) as ORDINAL)
{
}

lemma MinusThenPlusLess(o: ORDINAL, m: nat, n: nat)
  requires n <= m <= o.Offset
  ensures (o - (m as ORDINAL)) + (n as ORDINAL) == o - ((m - n) as ORDINAL)
{
}
