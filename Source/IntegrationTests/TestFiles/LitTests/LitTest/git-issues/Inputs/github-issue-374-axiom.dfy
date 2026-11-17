// Run by ../github-issue-374.dfy (https://github.com/erniecohen/dafny/issues/374).
// With --function-extensionality-axiom, an equality between total functions of one
// argument with the same exact domain follows from pointwise agreement, without a
// call.  With only --additional-axioms, every lemma here has exactly one error.

include "github-issue-374-decls.dfy"
import opened FunctionDomains

lemma IntLambdas() {
  var f := (x: int) => x + 1;
  var g := (y: int) => 1 + y;
  assert f == g;
}

lemma IntArguments(f: int -> int, g: int -> int)
  requires forall x :: f(x) == g(x)
  ensures f == g
{
}

lemma RealToBool(f: real -> bool, g: real -> bool)
  requires forall x :: f(x) == g(x)
  ensures f == g
{
}

lemma BoolArguments(f: bool -> int, g: bool -> int)
  requires forall x :: f(x) == g(x)
  ensures f == g
{
}

// ORDINAL cannot instantiate a type parameter, so only the axiom can state this.
lemma OrdinalArguments(f: ORDINAL -> int, g: ORDINAL -> int)
  requires forall x :: f(x) == g(x)
  ensures f == g
{
}

lemma NatRestrictions(f: nat -> int, g: nat -> int)
  requires forall x: nat :: f(x) == g(x)
  ensures Restrict<nat, int>(f) == Restrict<nat, int>(g)
{
}

datatype D = A | B
lemma DatatypeRestrictions() {
  var f := (x: D) => if x.A? then 1 else 2;
  var g := (x: D) => if x.B? then 2 else 1;
  assert Restrict(f) == Restrict(g);
}

// The axiom is triggered by an equality between the functions themselves, not by one
// inside a sequence, set or map; such an equality needs the functions' equality first.
lemma InSequence(f: int -> int, g: int -> int)
  requires forall x :: f(x) == g(x)
  ensures [f] == [g]
{
  assert f == g;
}
