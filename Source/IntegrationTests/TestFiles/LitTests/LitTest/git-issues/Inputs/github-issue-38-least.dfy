// Dafny 4.11.0 verifies this file. It should not. (Least-predicate mirror of one_is_two.dfy)
datatype S = N(o: ORDINAL) | Top

least predicate L(s: S) {
  forall t: S :: (t.N? && (s.Top? || t.o < s.o)) ==> L(t)
}

// Via the fixpoint equation: every state satisfies L (well-founded induction on the ordinal)
lemma All(s: S)
  ensures L(s)
  decreases s.Top?, if s.N? then s.o else 0
{
  forall t: S | t.N? && (s.Top? || t.o < s.o) ensures L(t) { All(t); }
}

// Via the stages: N(o) is absent from every stage k <= o, and Top from every stage
lemma NoStage(k: ORDINAL, s: S)
  requires s.Top? || k <= s.o
  ensures !L#[k](s)
{
  if k.IsLimit {
    forall m | m < k ensures !L#[m](s) { NoStage(m, s); }
  } else {
    NoStage(k - 1, N(k - 1));
  }
}

lemma OneIsTwo()
  ensures 1 == 2
{
  All(Top);
  forall k: ORDINAL ensures !L#[k](Top) { NoStage(k, Top); }
}
