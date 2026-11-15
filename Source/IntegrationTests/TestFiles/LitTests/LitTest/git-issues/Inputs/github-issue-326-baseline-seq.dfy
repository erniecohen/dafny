// Baseline for github-issue-326-route-eta.dfy: the same construction with a seq field, which has had its
// rank fact all along.  The termination check fails, and Bad verifies from the inconsistent definition.
datatype STree = SLeaf | SNode(kids: seq<STree>)
function G(n: nat): STree { SNode([G(n)]) }
lemma Bad() ensures false { assert G(0).kids[0] == G(0); }
