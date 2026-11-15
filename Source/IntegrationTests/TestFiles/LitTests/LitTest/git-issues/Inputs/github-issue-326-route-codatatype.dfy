// Codatatypes: an arrow field of a codatatype gets no rank fact, and a cycle through a codatatype is not
// below the inductive constructor around it.

codatatype S = SNil | SCons(f: nat -> S)
function SDepth(s: S): nat {
  if s.SNil? then 0 else SDepth(s.f(0)) + 1
}
function Loop(): S { SCons((n: nat) => Loop()) }
lemma BadCo() ensures false { assert Loop().f(0) == Loop(); }

datatype T = TLeaf | TNode(g: nat -> Co)
codatatype Co = CoNil | CoT(t: T)
lemma BadMixed(c: Co) requires c.CoT? && c.t.TNode? && c.t.g(0) == c ensures false {
  assert c.t.g(0) == c;
}
