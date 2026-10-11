// Dafny 4.11.0 verifies this file. It should not.
datatype S = N(o: ORDINAL) | Top

greatest predicate D(s: S) {
  exists t: S :: t.N? && (s.Top? || t.o < s.o) && D(t)
}

// Top survives every stage; N(o) survives every stage k <= o
lemma Stage(k: ORDINAL, s: S)
  requires s.Top? || k <= s.o
  ensures D#[k](s)
{
  if k.IsLimit {
    forall m | m < k ensures D#[m](s) { Stage(m, s); }
  } else {
    Stage(k - 1, N(k - 1));
  }
}

// but no N(o) is in the greatest fixpoint: its successors strictly descend
lemma NoN(o: ORDINAL)
  ensures !D(N(o))
{
  if D(N(o)) {
    var t: S :| t.N? && t.o < o && D(t);
    NoN(t.o);
  }
}

lemma OneIsTwo()
  ensures 1 == 2
{
  forall k: ORDINAL ensures D#[k](Top) { Stage(k, Top); }
  assert D(Top); // this shouldn't be established
  var t: S :| t.N? && D(t);   // unfold D(Top) once
  NoN(t.o);
}
