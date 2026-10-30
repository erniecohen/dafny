function abs(x: real): real { if x < 0.0 then -x else x }

// State the bound two-sided, without abs, inside the quantifier.
lemma TwoSided(l: seq<real>, d: real)
  requires d >= 0.0
  ensures forall i | 0 <= i < |l| :: -d <= l[i]-l[i] <= d
{}
