// Controls for git-issues/github-issue-354.dfy.

import opened Std.Ordinal
import Std.Axioms.Ordinals

// Vacuity: the premises, and the postconditions of the functions defined on them, do not prove
// false.
lemma Vacuity(left: ORDINAL, right: ORDINAL)
  ensures false
{
  Ordinals.Infinite();
  Ordinals.Replacement(iset{}, right);
  var w, p, t := Omega(), PlusLimit(left, right), TimesLimit(left, right);
}

// The premises are local: without a call, or a use of Omega, nothing says that an ordinal is
// not a natural number.
lemma NoInfinityWithoutACall()
  ensures exists o: ORDINAL :: !o.IsNat
{
}

// Replacement needs the graph of a function.
lemma NotAFunction()
{
  Ordinals.Replacement(iset{Ordinals.Pair(0, 0), Ordinals.Pair(0, 1)}, 1);
}
