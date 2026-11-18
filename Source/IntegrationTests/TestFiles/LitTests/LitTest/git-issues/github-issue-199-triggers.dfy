// RUN: %verify --type-system-refresh --lemma-bindings --show-hints "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/199: a bound result of a lemma is a term that a quantifier can be
// triggered on, as a function application is.  It is shown, and can be written, as the expression that binds
// the results of the call and returns that one.

lemma Find(n: nat) returns (m: nat)
  ensures m > n
{
  m := n + 1;
}

lemma Split(n: nat) returns (a: nat, b: nat)
  ensures a + b == n
{
  a, b := 0, n;
}

ghost function Above(n: nat): nat {
  var m := Find(n); m
}

lemma OneResult()
  ensures forall n: nat :: (var m := Find(n); m) > n
{
}

lemma TwoResults()
  ensures forall n: nat :: (var a, b := Split(n); a + b) == n
{
}

lemma Written()
  ensures forall n: nat {:trigger var m := Find(n); m} :: (var m := Find(n); m) > n
{
}

// The quantifier in the precondition is instantiated by the term Above(i) unfolds to.
lemma Instantiate(s: seq<nat>, i: nat)
  requires forall n: nat {:trigger var m := Find(n); m} :: n < |s| ==> s[n] == (var m := Find(n); m)
  requires i < |s|
  ensures s[i] == Above(i)
{
}

lemma WithAnotherTerm(s: seq<nat>)
  requires forall i :: 0 <= i < |s| ==> s[i] == (var m := Find(i); m)
  ensures forall i :: 0 <= i < |s| ==> s[i] > i
{
}

lemma Further(n: nat) returns (m: nat)
  ensures m > n + 1
{
  m := n + 2;
}

// Results of different lemmas at the same arguments are different terms.
lemma TwoLemmas()
  ensures forall n: nat :: (var m := Find(n); m) + (var m := Further(n); m) > 2 * n
{
}

ghost predicate Positive(n: nat) {
  n > 0
}

// A binding has a statement in it, the call. So, as for any expression with a statement in it, a term that
// contains the binding is not a trigger. The result that the binding returns is.
lemma InsideAnotherTerm()
  ensures forall n: nat :: Positive(var m := Find(n); m)
{
}
