// RUN: %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Valid downcasts of a general trait's value to a datatype with invariants verify, and the result satisfies the
// invariants (https://github.com/erniecohen/dafny/issues/550). The invalid ones are in git-issue-550-trait-downcast.dfy.

trait G { const n: int  invariant true }
datatype D extends G = D(m: int) {
  invariant m > 0
}

method Valid(g: G) returns (d: D)
  requires g is D
  ensures d.m > 0
{
  d := g as D;
}

method FromRaw() {
  var r: raw D := D(1);
  var rg: raw G := r;
  var g: G := rg as G;
  var d := g as D;
  assert d.m == 1;
}

method RoundTrip(d: D) {
  var g: G := d;
  var d' := g as D;
  assert d' == d;
}

// The other direction: a raw value of the datatype, which is not boxed, reaches the trait, which checks the invariants
// of the trait on the boxed value
method UpFromRaw() {
  var r: raw D := D(1);
  var g: G := r as G;
  var d := g as D;
  assert d.m == 1;
}
