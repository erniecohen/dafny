// A field of type A -> (B -> T): no rank fact for the inner application, so the recursion through it fails,
// and the eta-expanded self reference fails its termination check.

datatype N = NLeaf | NNode(f: nat -> (nat -> N))
function NSize(t: N): nat {
  if t.NLeaf? then 0 else NSize(t.f(0)(1)) + 1
}
function Self(n: nat): nat -> N { (m: nat) => NNode((k: nat) => Self(k)) }
lemma Bad() ensures false { assert Self(0)(1) == NNode((k: nat) => Self(k)); assert Self(0)(1).f(0)(1) == Self(0)(1); }
