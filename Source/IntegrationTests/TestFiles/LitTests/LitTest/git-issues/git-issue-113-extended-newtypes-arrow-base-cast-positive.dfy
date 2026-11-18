// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// Valid controls for git-issue-113-extended-newtypes-arrow-base-cast-negative.

newtype F = int -> nat witness *
newtype Outer = F witness *
newtype G<T> = int -> T witness *

method Valid(f: int -> nat, n: F) {
  var a := ((x: int) => 1) as F;
  var b := ((x: int) => x * x) as F;
  var c := f as F;
  var d := ((x: int) => 2) as Outer;
  var e := ((x: int) => 3) as G<nat>;
  var e' := ((x: int) => -3) as G<int>;
  var k := n as F;
  assert (a as int -> nat)(7) == 1;
  assert (d as F as int -> nat)(0) == 2;
}
