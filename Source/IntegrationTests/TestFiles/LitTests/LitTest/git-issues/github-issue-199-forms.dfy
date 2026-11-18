// RUN: %exits-with 2 %resolve --lemma-bindings "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/199: with --lemma-bindings, "var y1, ..., yk := L(args); e" binds
// the results of the lemma L.  This file has the forms that are not such a binding.  Each is an error, and none
// of them is accepted as something else.

method M(n: nat) returns (m: nat)
twostate lemma TwoState(n: nat) returns (m: nat)
least predicate P(n: nat) { true }
least lemma Least(n: nat)
  requires P(n)
greatest predicate Q(n: nat) { true }
greatest lemma {:axiom} Greatest(n: nat)
  ensures Q(n)
lemma Pair(n: nat) returns (p: (nat, nat))
lemma Two(n: nat) returns (a: nat, b: nat)
lemma None(n: nat)
lemma {:axiom} Find(n: nat) returns (m: nat)
  ensures m > n
datatype Result = Success(value: nat) | Failure {
  predicate IsFailure() { Failure? }
  function PropagateFailure(): Result { this }
  function Extract(): nat requires Success? { value }
}
lemma Fallible(n: nat) returns (r: Result)

// ----- Only an ordinary lemma.  (A least or greatest lemma has no results, and neither has its prefix lemma.)

ghost function Method(n: nat): nat {
  var m := M(n); m  // error: a method
}

twostate function TwoStateLemma(n: nat): nat {
  var m := TwoState(n); m  // error: a two-state lemma
}

ghost function LeastLemma(n: nat): nat
  requires P(n)
{
  var m := Least(n); n  // error: a least lemma
}

ghost function GreatestLemma(n: nat): nat {
  var m := Greatest(n); n  // error: a greatest lemma
}

ghost function PrefixLemma(n: nat, k: ORDINAL): nat
  requires P#[k](n)
{
  var m := Least#[k](n); n  // error: a prefix lemma
}

// ----- Only variables on the left, one for each result, and one call on the right

ghost function Pattern(n: nat): nat {
  var (a, b) := Pair(n); a  // error: a pattern
}

ghost function TwoCalls(n: nat): nat {
  var a, b := Find(n), Find(n); a  // error: two right-hand sides
}

ghost function TooFew(n: nat): nat {
  var a := Two(n); a  // error: one variable for two results
}

ghost function TooMany(n: nat): nat {
  var a, b := Find(n); a  // error: two variables for one result
}

ghost function NoResults(n: nat): nat {
  var a := None(n); n  // error: one variable for no results
}

// ----- ":-" does not bind the results of a lemma

ghost function ElephantOperator(n: nat): Result {
  var a :- Fallible(n); Success(a)  // error: a lemma call in an expression
}
