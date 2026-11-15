// Controls for ../github-issue-374.dfy (https://github.com/erniecohen/dafny/issues/374).
// Every lemma here has exactly one error, with function domains on, with or without
// the extensionality axiom.

include "github-issue-374-decls.dfy"
import opened FunctionDomains

// The counterexample's functions themselves are not equal.
lemma GenericNotEqual() {
  var f: nat -> int := (x: int) => if x < 0 then 7 else x;
  var g: nat -> int := (x: int) => x;
  assert f == g; // error
}

// A lambda over int, viewed as a nat -> int, does not have nat as its exact domain.
lemma NotExact() {
  var f: nat -> int := (x: int) => x;
  assert ExactDom<nat, int>(f); // error
}

// No exact domain for a type parameter.
lemma GenericExt<T>(f: T -> int, g: T -> int)
  requires forall x :: f(x) == g(x)
  ensures f == g // error
{
}

// Vacuity: assert false still fails after live uses of each fact.
lemma VacuityNat() {
  var f: nat -> int := (x: nat) => x;
  var g: nat -> int := (x: nat) => if x >= 0 then x else 0;
  FunExt<nat, int>(f, g);
  assert f == g;
  assert false; // error
}

lemma VacuityInt(f: int -> int, g: int -> int)
  requires forall x :: f(x) == g(x)
{
  FunExt(f, g);
  assert false; // error
}

lemma VacuityGeneric() {
  var f: nat -> int := (x: int) => if x < 0 then 7 else x;
  var g: nat -> int := (x: int) => x;
  FunExt<nat, int>(Restrict<nat, int>(f), Restrict<nat, int>(g));
  assert Restrict<nat, int>(f) == Restrict<nat, int>(g);
  assert false; // error
}

// Equating the restrictions of two datatype lambdas that differ at a box of another
// datatype is consistent, because the lambdas' application axioms hold only on their
// domain.
datatype D = A | B
datatype E = E0
lemma VacuityDatatype() {
  var f := (x: D) => if x.A? then 1 else 2;
  var g := (x: D) => if x.B? then 2 else 1;
  FunExt(Restrict(f), Restrict(g));
  assert Restrict(f) == Restrict(g);
  assert false; // error
}

lemma VacuityRestrict<A, B>(f: A -> B) {
  assert ExactDom(Restrict(f));
  assert Restrict(Restrict(f)) == Restrict(f);
  assert false; // error
}

// The selectors of F#Handle and of a lambda over int are unguarded, but extensionality
// stays consistent: a named function is constrained only inside its domain, and a lambda
// over int reads every box as an int.
function F(x: D): int { if x.A? then 1 else 2 }
function G(x: D): int { if x.B? then 2 else 1 }
lemma VacuityNamedDatatype() {
  FunExt(F, G);
  assert F == G;
  assert false; // error
}

lemma VacuityLambdaNamed() {
  var f := (x: D) => if x.A? then 1 else 2;
  FunExt(f, G);
  assert f == G;
  assert false; // error
}

function N(x: nat): int { x }
function M(x: nat): int { if x >= 0 then x else 5 }
lemma VacuityNamedSubset() {
  FunExt<nat, int>(N, M);
  assert N == M;
  assert false; // error
}

function Inc(x: int): int { x + 1 }
lemma VacuityIntLambdas() {
  var f := (x: int) => if x < 0 then -x else x;
  var g := (x: int) => if x >= 0 then x else -x;
  FunExt(f, g);
  assert f == g;
  var h := (x: int) => x + 1;
  FunExt(h, Inc);
  assert h == Inc;
  assert false; // error
}
