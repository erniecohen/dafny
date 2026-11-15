// Controls for Std.Axioms.FunctionExtensionality
// (https://github.com/erniecohen/dafny/issues/34), run by ../github-issue-34.dfy.
// Every lemma here but Ext has exactly one error.

import opened Std.Axioms.FunctionExtensionality

// A program that does not call the lemmas is unaffected: extensionality does not hold.
lemma NotCalled(f: int -> int, g: int -> int)
  requires forall x :: f(x) == g(x)
  ensures f == g // error
{
}

// Vacuity: a live call on agreeing functions leaves `assert false` failing.
lemma VacuityInt(f: int -> int, g: int -> int)
  requires forall x :: f(x) == g(x)
{
  OnInt(f, g);
  assert f == g;
  assert false; // error
}

lemma VacuityNullary() {
  Nullary(() => 3, () => 1 + 2);
  assert false; // error
}

lemma VacuityIntInt() {
  OnIntInt((x: int, y: int) => x * y, (x: int, y: int) => y * x);
  assert false; // error
}

// The fact is about all of the domain.  Two nat -> int values that agree on every nat
// are also int ~> int values, and these two differ at -1: the call's precondition fails.
// (The call goes through Ext so that the error's related location is in this file.)
lemma Ext(f: int -> int, g: int -> int)
  requires forall x: int :: f(x) == g(x)
  ensures f == g
{
  OnInt(f, g);
}

lemma SubsetDomain() {
  var f: nat -> int := (x: int) => if x < 0 then 7 else x;
  var g: nat -> int := (x: int) => x;
  assert forall n: nat :: f(n) == g(n);
  Ext(f, g); // error: precondition
}

// A partial arrow is not a total one: the argument fails the subset constraint of int -> int.
lemma Partial() {
  var p: int --> int := (x: int) requires x >= 0 => x;
  OnInt(p, (x: int) => x); // error: p is not an int -> int
}

// Nor is a heap-reading arrow.
class Cell { var v: int }

lemma HeapReading(c: Cell) {
  var h: int ~> int := (x: int) reads c => x;
  OnInt(h, (x: int) => x); // error: h is not an int -> int
}

// Partial functions that agree wherever both are defined are not thereby equal.
lemma PartialAgreement() {
  var p: int --> int := (x: int) requires x >= 0 => x;
  var q: int --> int := (x: int) requires x >= 0 => x + 0;
  assert forall x | p.requires(x) && q.requires(x) :: p(x) == q(x);
  assert p == q; // error
}
