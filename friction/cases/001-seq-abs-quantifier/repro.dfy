function abs(x: real): real { if x < 0.0 then -x else x }

lemma Scalar(x: real, d: real) requires d >= 0.0 ensures abs(x-x) <= d {}

lemma Indexed(l: seq<real>, d: real)
  requires d >= 0.0
  ensures forall i | 0 <= i < |l| :: abs(l[i]-l[i]) <= d
{}

lemma TwoSided(l: seq<real>, d: real)
  requires d >= 0.0
  ensures forall i | 0 <= i < |l| :: -d <= l[i]-l[i] <= d
{}
