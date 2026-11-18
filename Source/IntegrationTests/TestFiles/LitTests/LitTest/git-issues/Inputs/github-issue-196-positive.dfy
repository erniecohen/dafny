// Universal quantifiers whose body the solver simplifies until no trigger term
// is left: l[i] - l[i] becomes 0.0, so the call facts of abs, assumed under the
// trigger Seq#Index(l, i), were never instantiated for the checked instance.

function abs(x: real): real { if x < 0.0 then -x else x }

predicate near(s: seq<real>, t: seq<real>, d: real)
  requires |s| == |t|
{
  forall i | 0 <= i < |s| :: abs(s[i] - t[i]) <= d
}

lemma Scalar(x: real, d: real) requires d >= 0.0 ensures abs(x - x) <= d {}

lemma Indexed(l: seq<real>, d: real)
  requires d >= 0.0
  ensures forall i | 0 <= i < |l| :: abs(l[i] - l[i]) <= d
{}

lemma ForallStatement(l: seq<real>, d: real)
  requires d >= 0.0
  ensures forall i | 0 <= i < |l| :: abs(l[i] - l[i]) <= d
{
  forall i | 0 <= i < |l| ensures abs(l[i] - l[i]) <= d {
    Scalar(l[i], d);
  }
}

lemma Times(l: seq<real>, d: real)
  requires d >= 0.0
  ensures forall i | 0 <= i < |l| :: abs(l[i] * 0.0) <= d
{}

lemma Sum(l: seq<real>, m: seq<real>, d: real)
  requires d >= 0.0 && |l| == |m|
  ensures forall i | 0 <= i < |l| :: abs((l[i] + m[i]) - (m[i] + l[i])) <= d
{}

// The quantifier comes from the inlined body of near.
lemma NearRefl(s: seq<real>, d: real)
  requires d >= 0.0
  ensures near(s, s, d)
{}

method AssertStatement(l: seq<real>, d: real)
  requires d >= 0.0
{
  assert forall i | 0 <= i < |l| :: abs(l[i] - l[i]) <= d;
}

// Bound variables of a datatype or class type, whose type antecedents the option
// must rebind to the checked quantifier. These verify with and without it.
datatype Point = Point(x: real)

lemma DatatypeField(ps: set<Point>, d: real)
  requires d >= 0.0
  ensures forall p | p in ps :: abs(p.x - p.x) <= d
{}

class Cell { var v: real }

method ClassField(cs: set<Cell>, d: real)
  requires d >= 0.0
  ensures forall c | c in cs :: abs(c.v - c.v) <= d
{}

// These verified before, and still do.
lemma NearSym(s: seq<real>, t: seq<real>, d: real)
  requires |s| == |t| && near(s, t, d)
  ensures near(t, s, d)
{}

lemma TwoSided(l: seq<real>, d: real)
  requires d >= 0.0
  ensures forall i | 0 <= i < |l| :: -d <= l[i] - l[i] <= d
{}
