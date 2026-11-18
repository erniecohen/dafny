// RUN: %exits-with 4 %verify --lemma-bindings "%s" > "%t"
// RUN: %exits-with 4 %verify --lemma-bindings --default-function-opacity:opaque "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/199: what is known about the bound results of a lemma is the
// lemma's postcondition, and it becomes known where the binding is evaluated.  So it is hidden with the body of
// an opaque function that contains the binding, and an opaque predicate in the postcondition stays opaque.
// In the second run every function is opaque, so each lemma that does not reveal AboveByGreater fails as well.

lemma Find(n: nat) returns (m: nat)
  ensures m > n
{
  m := n + 1;
}

opaque ghost function Above(n: nat): nat {
  var m := Find(n); m
}

lemma AboveIsHidden(n: nat)
  ensures Above(n) > n  // error: the body of Above is not revealed
{
}

lemma AboveRevealed(n: nat)
  ensures Above(n) > n
{
  reveal Above();
}

opaque ghost predicate Greater(n: nat, m: nat) {
  m > n
}

lemma FindGreater(n: nat) returns (m: nat)
  ensures Greater(n, m)
{
  reveal Greater();
  m := n + 1;
}

ghost function AboveByGreater(n: nat): nat {
  var m := FindGreater(n); m
}

lemma PostconditionIsKnown(n: nat)
  ensures Greater(n, AboveByGreater(n))  // (an error in the second run)
{
}

lemma GreaterIsHidden(n: nat)
  ensures AboveByGreater(n) > n  // error: Greater is not revealed
{
}

lemma GreaterRevealed(n: nat)
  ensures AboveByGreater(n) > n  // (an error in the second run)
{
  reveal Greater();
}

lemma HideAll(n: nat)
  ensures Above(n) > n
{
  hide *;
  reveal Above();
}

lemma HideAllButTheBinder(n: nat)
  ensures AboveByGreater(n) > n  // error: Greater is not revealed
{
  hide *;
  reveal AboveByGreater();
}

lemma HideTheBinder(n: nat)
  ensures Greater(n, AboveByGreater(n))  // error: the body of AboveByGreater is not revealed
{
  hide *;
}
