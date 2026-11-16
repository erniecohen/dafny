// RUN: %verify --type-system-refresh --general-newtypes --extended-newtype-bases "%s" > "%t"
// RUN: %exits-with 2 %resolve "%S/Inputs/github-issue-422-underspecified.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Regression (https://github.com/erniecohen/dafny/issues/422): under the type system refresh, an operator
// in the witness of a newtype without a constraint variable was resolved only after the pass that reads it.
// A Debug build of dafny failed the assertion in BinaryExpr.ResolvedOp and stopped on this file, and did
// not report the underspecified witness on line 4 of Inputs/github-issue-422-underspecified.dfy (alone, that
// declaration failed a cast).  A Release build, which CI runs, gives the same output with and without the
// fix; CI checks the fix in a Debug build with DafnyCore.Test's NewtypeWitnessResolutionTests.

newtype N = int witness 1 + 1
newtype Inc = int -> int witness ((x: int) => x + 1)
newtype Binary = (int, int) -> int witness ((x: int, y: int) => x + y)
newtype Small = int -> bool witness ((x: int) => 0 <= x && x < 10)

// The same witnesses with a constraint variable, which were already checked.
newtype M = m: int | true witness 1 + 1
newtype IncWithVariable = f: int -> int | true witness ((x: int) => x + 1)

method Use(inc: Inc, add: Binary) returns (r: int)
  requires inc(2) == 3 && add(2, 0) == 2
  ensures r == 5
{
  var n: N := 2;
  var m: M := 2;
  r := inc(n as int) + add(m as int, 0);
}

method Convert() {
  var small := ((x: int) => 0 <= x && x < 10) as Small;
  var inc := ((x: int) => x + 1) as IncWithVariable;
  assert small(3) && !small(10);
  assert inc(2) == 3;
}
