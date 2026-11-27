// RUN: python3 "%repositoryRoot/.github/review/quantifier-resolution-oracle.py" "%binaryDir/Dafny.dll" "%review-z3" "%s" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
module Cached {
  lemma Positive(s: seq<int>)
    ensures forall i :: 0 <= i < |s| ==> s[i] == s[i]
  {}
  lemma Counterexample(s: seq<int>) {
    assert forall i :: 0 <= i < |s| ==> s[i] == 0;
  }
}
