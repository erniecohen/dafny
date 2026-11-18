// RUN: %exits-with 4 %verify --type-system-refresh:false --general-traits:legacy --general-newtypes:false "%s" > "%t"
// RUN: %exits-with 4 %verify "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/362: a let-such-that condition that reads no heap,
// but contains a lambda that calls a function, crashed the verifier with "undeclared identifier:
// $heap".  The axiom of the let's $canCall function translated the lambda's CanCall facts with a
// heap variable that the axiom binds only when the condition reads the heap.

ghost predicate Q(f: int -> int) { true }
function P(x: int): int { x }

// The reproduction from the issue.
ghost function G(): int
{
  var u :| Q(x => P(x)) && u == 0;
  u
}

// The same let-such-that in a method and in a function that reads the heap elsewhere.
method M() {
  ghost var y := var u :| Q(x => P(x)) && u == 0; u;
  assert y == 0;
}

class C {
  var fld: int
  ghost function R(): int reads this { var u :| Q(x => P(x)) && u == fld; u }
  method UseR() requires fld == 3 { assert R() == 3; }
}

// The lambda calls a function with a precondition, and the function containing the let calls itself.
function Pos(x: int): int requires x >= 0 { x }
ghost function F(n: nat): int decreases n
{
  var u :| Q(x => if 0 <= x < n then Pos(x) + F(x) else 0) && u == n;
  u
}

lemma UseG() ensures G() == 0 && F(4) == 4 { }

// Controls: the let's condition still determines its value, and nothing false follows.
lemma WrongG() ensures G() == 1 { }  // error
lemma NoContradiction() ensures false { var g := G(); }  // error
