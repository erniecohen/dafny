// Algebraic obligations, quantified over arbitrary component values and suffixes.
lemma RealTupleAbsorption(x: real, y: real, z: real, a: int, b: int, c: int)
  ensures (y, b decreases to x, a) && (z, c nonincreases to y, b) ==> (z, c decreases to x, a)
  ensures (y, b nonincreases to x, a) && (z, c decreases to y, b) ==> (z, c decreases to x, a)
  ensures (y, b nonincreases to x, a) && (z, c nonincreases to y, b) ==> (z, c nonincreases to x, a)
  ensures (y, b decreases to x, a) ==> (y, b nonincreases to x, a)
{}

datatype D = Leaf | Node(tail: D)
lemma MixedRankAbsorption(x: seq<D>, y: D, z: seq<D>, a: int, b: int, c: int)
  ensures (y, b decreases to x, a) && (z, c nonincreases to y, b) ==> (z, c decreases to x, a)
  ensures (y, b nonincreases to x, a) && (z, c decreases to y, b) ==> (z, c decreases to x, a)
  ensures (y, b nonincreases to x, a) && (z, c nonincreases to y, b) ==> (z, c nonincreases to x, a)
{}

function IntHeight(x: int): nat { if x < 0 then 0 else x + 1 }
function RealHeight(x: real): nat { if x < 0.0 then 0 else x.Floor + 1 }
lemma NumericWellFounded(x: int, y: int, r: real, s: real)
  ensures (y decreases to x) ==> IntHeight(x) < IntHeight(y)
  ensures (s decreases to r) ==> RealHeight(r) < RealHeight(s)
{}

lemma OpaqueWeakIdentity<T>(x: T)
  ensures (x nonincreases to x)
{}
