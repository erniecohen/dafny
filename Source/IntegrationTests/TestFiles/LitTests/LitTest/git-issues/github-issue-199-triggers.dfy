// RUN: %verify --lemma-bindings --show-hints "%s" > "%t"
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
