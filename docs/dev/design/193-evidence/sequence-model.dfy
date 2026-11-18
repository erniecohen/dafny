// Design-only verified connection between the recursive natural references and
// the little-endian seq<bool> view. No new axioms or assume statements.

ghost function andNat(w: nat, a: nat, b: nat): nat
  decreases w
{ if w == 0 then 0 else (a % 2) * (b % 2) + 2 * andNat(w - 1, a / 2, b / 2) }

ghost function orNat(w: nat, a: nat, b: nat): nat
  decreases w
{ if w == 0 then 0 else (if a % 2 == 1 || b % 2 == 1 then 1 else 0) + 2 * orNat(w - 1, a / 2, b / 2) }

ghost function xorNat(w: nat, a: nat, b: nat): nat
  decreases w
{ if w == 0 then 0 else (if a % 2 != b % 2 then 1 else 0) + 2 * xorNat(w - 1, a / 2, b / 2) }

ghost function andNotNat(w: nat, a: nat, b: nat): nat
  decreases w
{ if w == 0 then 0 else (if a % 2 == 1 && b % 2 == 0 then 1 else 0) + 2 * andNotNat(w - 1, a / 2, b / 2) }

ghost function bits(w: nat, a: nat): seq<bool>
  ensures |bits(w,a)| == w
  decreases w
{ if w == 0 then [] else [a % 2 == 1] + bits(w - 1, a / 2) }

ghost function value(s: seq<bool>): nat
  decreases |s|
{ if |s| == 0 then 0 else (if s[0] then 1 else 0) + 2 * value(s[1..]) }

ghost predicate opbit(op: nat, a: bool, b: bool)
  requires op < 4
{ if op == 0 then a && b else if op == 1 then a || b else if op == 2 then a != b else a && !b }

ghost function zip(op: nat, a: seq<bool>, b: seq<bool>): seq<bool>
  requires op < 4 && |a| == |b|
  ensures |zip(op,a,b)| == |a|
  decreases |a|
{ if |a| == 0 then [] else [opbit(op,a[0],b[0])] + zip(op,a[1..],b[1..]) }

ghost function reference(w: nat, op: nat, a: nat, b: nat): nat
  requires op < 4
{ if op == 0 then andNat(w,a,b) else if op == 1 then orNat(w,a,b) else if op == 2 then xorNat(w,a,b) else andNotNat(w,a,b) }

lemma {:induction false} view(w: nat, op: nat, a: nat, b: nat)
  requires op < 4
  ensures value(zip(op,bits(w,a),bits(w,b))) == reference(w,op,a,b)
  decreases w
{
  if w > 0 {
    view(w-1,op,a/2,b/2);
    assert a % 2 == 0 || a % 2 == 1;
    assert b % 2 == 0 || b % 2 == 1;
  }
}

lemma {:induction false} view8(a: nat, b: nat, op: nat)
  requires op < 4
  ensures value(zip(op,bits(8,a),bits(8,b))) == reference(8,op,a,b)
{ view(8,op,a,b); }

lemma {:induction false} view16(a: nat, b: nat, op: nat)
  requires op < 4
  ensures value(zip(op,bits(16,a),bits(16,b))) == reference(16,op,a,b)
{ view(16,op,a,b); }

lemma {:induction false} view32(a: nat, b: nat, op: nat)
  requires op < 4
  ensures value(zip(op,bits(32,a),bits(32,b))) == reference(32,op,a,b)
{ view(32,op,a,b); }

lemma {:induction false} view64(a: nat, b: nat, op: nat)
  requires op < 4
  ensures value(zip(op,bits(64,a),bits(64,b))) == reference(64,op,a,b)
{ view(64,op,a,b); }
