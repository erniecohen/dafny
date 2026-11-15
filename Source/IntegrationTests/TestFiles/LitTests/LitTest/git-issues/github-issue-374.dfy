// RUN: %exits-with 4 %verify --additional-axioms=false --show-snippets=false --allow-axioms "%s" > "%t"
// RUN: %verify --additional-axioms=true --show-snippets=false --allow-axioms "%s" >> "%t"
// RUN: %verify --function-extensionality-axiom --show-snippets=false --allow-axioms "%s" >> "%t"
// RUN: %exits-with 4 %verify --additional-axioms=true --show-snippets=false --allow-axioms "%S/Inputs/github-issue-374-negative.dfy" >> "%t"
// RUN: %exits-with 4 %verify --function-extensionality-axiom --show-snippets=false --allow-axioms "%S/Inputs/github-issue-374-negative.dfy" >> "%t"
// RUN: %exits-with 4 %verify --additional-axioms=true --show-snippets=false --allow-axioms "%S/Inputs/github-issue-374-axiom.dfy" >> "%t"
// RUN: %verify --function-extensionality-axiom --show-snippets=false --allow-axioms "%S/Inputs/github-issue-374-axiom.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Function domains (https://github.com/erniecohen/dafny/issues/374), with extensionality
// as the lemma FunExt.  With function domains off, every FunExt call fails its
// precondition and Restrict and ExactDom say nothing.

include "Inputs/github-issue-374-decls.dfy"
import opened FunctionDomains

// The counterexample that rules out extensionality over a type parameter: f and g agree
// on nat but differ at -1.  Only their restrictions to nat are equal.
lemma Generic() {
  var f: nat -> int := (x: int) => if x < 0 then 7 else x;
  var g: nat -> int := (x: int) => x;
  FunExt<nat, int>(Restrict<nat, int>(f), Restrict<nat, int>(g));
  assert Restrict<nat, int>(f) == Restrict<nat, int>(g);
}

// Lambdas over nat that agree pointwise are equal: their domain is exactly nat.
lemma NatLambdas() {
  var f: nat -> int := (x: nat) => x;
  var g: nat -> int := (x: nat) => if x >= 0 then x else 0;
  FunExt<nat, int>(f, g);
  assert f == g;
}

// A restriction has an exact domain, and restricting a function with an exact
// domain gives the function back.
lemma RestrictExact<A, B>(f: A -> B)
  ensures ExactDom(Restrict(f))
  ensures ExactDom(f) <==> Restrict(f) == f
{
}

// Every int arrow has an exact domain.
lemma IntExt(f: int -> int, g: int -> int)
  requires forall x :: f(x) == g(x)
  ensures f == g
{
  FunExt(f, g);
}

// A restriction agrees with the function on its domain.
lemma RestrictApply(f: nat -> int, x: nat)
  ensures Restrict<nat, int>(f)(x) == f(x)
{
}

// Over a datatype: lambdas that agree on D still differ at boxes of other types, which
// the restrictions forget.
datatype D = A | B
lemma DatatypeRestrict() {
  var f := (x: D) => if x.A? then 1 else 2;
  var g := (x: D) => if x.B? then 2 else 1;
  FunExt(Restrict(f), Restrict(g));
  assert Restrict(f) == Restrict(g);
}

// A named function has its formals' types as its exact domain, as does a lambda over int.
function F(x: D): int { if x.A? then 1 else 2 }
function G(x: D): int { if x.B? then 2 else 1 }
function Inc(x: int): int { x + 1 }
lemma NamedFunctions() {
  FunExt(F, G);
  assert F == G;
  var f := (x: D) => if x.A? then 1 else 2;
  FunExt(f, G);
  assert f == G;
  var h := (x: int) => x + 1;
  FunExt(h, Inc);
  assert h == Inc;
}
