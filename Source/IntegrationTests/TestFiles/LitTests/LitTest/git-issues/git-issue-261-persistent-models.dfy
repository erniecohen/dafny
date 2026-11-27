// Native model data and current source state descriptions from persistent module results.
// RUN: python3 "%repositoryRoot/.github/review/persistent-models.py" "%binaryDir/Dafny.dll" "%review-z3" "%s" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
include "contract.dfy"
module Other { lemma Good() { assert 1 + 1 == 2; } }
module Witness {
  datatype D = Empty | Node(head: int, tail: seq<int>)
  class Box { var value: int }
  method Values(x: int, b: Box, a: array<int>, s: seq<int>, m: map<int, int>, d: D, c: char, v: bv8)
    requires a.Length > 0 && |s| > 0 && 0 in m
  {
    var y := x;
    y := y + 1;
    assert y < 0 && a[0] == m[0] && s[0] == 2 && d == Empty && c == 'a' && v == 1 && b.value == 42;
  }
  method Loop(x: int) {
    var y := x;
    while y > 0
      decreases y
    { y := y - 1; }
    assert y == 1;
  }
  lemma Multiple(x: int, y: int) { assert x > 0; assert y > 0; }
}
