// With or without --quantified-check-call-facts, none of these verifies.

function abs(x: real): real { if x < 0.0 then -x else x }

predicate near(s: seq<real>, t: seq<real>, d: real)
  requires |s| == |t|
{
  forall i | 0 <= i < |s| :: abs(s[i] - t[i]) <= d
}

function inv(x: real): real requires x != 0.0 { 1.0 / x }

lemma Wrong(l: seq<real>, d: real)
  requires d >= 0.0
  ensures forall i | 0 <= i < |l| :: abs(l[i] - l[i]) > d
{}

lemma Positive(l: seq<real>)
  ensures forall i | 0 <= i < |l| :: abs(l[i]) > 0.0
{}

lemma NotNear(s: seq<real>, d: real)
  requires d >= 0.0 && |s| > 0
  ensures !near(s, s, d)
{}

datatype Point = Point(x: real)

lemma DatatypeField(ps: set<Point>, d: real)
  requires d >= 0.0
  ensures forall p | p in ps :: abs(p.x - p.x) > d
{}

// The checked quantifier proves what it should, and nothing more.
method Vacuity(l: seq<real>, d: real)
  requires d >= 0.0
{
  assert forall i | 0 <= i < |l| :: abs(l[i] - l[i]) <= d;
  assert false;
}

// A call outside its precondition is still reported, and its call facts do
// not prove the body.
lemma OutsidePrecondition(l: seq<real>)
  ensures forall i | 0 <= i < |l| :: inv(l[i] - l[i]) == 0.0
{}
