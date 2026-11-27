// RUN: python3 "%repositoryRoot/.github/review/cache-stack-acceptance.py" "%binaryDir/Dafny.dll" "%review-z3" "%s" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
module Cached {
  lemma Positive(s: seq<int>) { assert forall i :: 0 <= i < |s| ==> s[i] == 0; }
}
