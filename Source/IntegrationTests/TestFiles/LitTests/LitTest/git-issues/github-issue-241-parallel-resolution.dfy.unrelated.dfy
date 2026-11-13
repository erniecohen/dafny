// RUN: %baredafny resolve --resolution-threads 8 %s > "%t"
include "github-issue-241-parallel-resolution.dfy.valid.dfy"
module Unrelated {
  function F(x: bv77): (int, bool, int, int, int) { (0, true, 0, 0, 0) }
  lemma Good() ensures true { }
}
