// Paired real-solver runs with per-module output and logs; not a singleton verdict-planner command.
// RUN: python3 "%repositoryRoot/.github/review/translation-copy.py" "%t.runs" "%binaryDir/Dafny.dll" "%review-z3" "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// UNSUPPORTED: windows

// https://github.com/erniecohen/dafny/issues/241
module A {
  datatype D = Zero | Value(n: int)
  function F(x: int): int { x + 1 }
  function Closure(): int -> int { F }
}
module B {
  import X = A
  lemma Good(x: int) ensures X.Closure()(x) == x + 1 {}
  lemma Aliases(d: X.D) ensures d.Value? ==> d == X.Value(d.n) {}
  method Array(f: int -> int) returns (a: array<int>)
    requires forall x :: 0 <= x < 3 ==> f.requires(x)
    ensures a.Length == 3
  { a := new int[3](f); }
  lemma Bad() { assert false; }
}
