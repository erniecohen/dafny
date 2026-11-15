ghost function End(s: nat, p: seq<nat>): nat decreases |p| { if |p| == 0 then s else End(p[0], p[1..]) }
ghost predicate P(R: set<nat>, s: nat) { forall v :: v in R ==> exists p :: End(s, p) == v }

lemma Id(R: set<nat>, s: nat)
  requires forall v :: v in R ==> exists p :: End(s, p) == v
  ensures  forall v :: v in R ==> exists p :: End(s, p) == v
{}

lemma IdWrapped(R: set<nat>, s: nat) requires P(R, s) ensures P(R, s) {}
