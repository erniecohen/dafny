// Run by ../github-issue-374.dfy (https://github.com/erniecohen/dafny/issues/374).
// Positive controls for the handle axioms that the main test does not exercise: each
// lemma verifies, with function domains on, only because of the axiom named before it.

include "github-issue-374-decls.dfy"
import opened FunctionDomains

datatype D = A | B
class C {}

// The precondition selector of TagHandle1 (a lambda over a datatype).
lemma TagRequires() {
  var f := (x: D) requires x.A? => 1;
  assert f.requires(A);
}

// The reads selector of TagHandle1.
lemma TagReads(c: C) {
  var f := (x: D) reads c => 1;
  assert c in f.reads(A);
}

// The precondition selector of BasicHandle1 (a lambda over int).
lemma BasicRequires() {
  var f := (x: int) requires x > 0 => 1;
  assert f.requires(1);
}

// The reads selector of BasicHandle1.
lemma BasicReads(c: C) {
  var f := (x: int) reads c => 1;
  assert c in f.reads(0);
}

// Every bool arrow, and every real arrow, has an exact domain.
lemma BoolExt(f: bool -> int, g: bool -> int)
  requires forall x :: f(x) == g(x)
  ensures f == g
{
  FunExt(f, g);
}

lemma RealExt(f: real -> int, g: real -> int)
  requires forall x :: f(x) == g(x)
  ensures f == g
{
  FunExt(f, g);
}
