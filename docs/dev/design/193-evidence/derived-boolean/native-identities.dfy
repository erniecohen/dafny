// Native Boolean identities only: no integer bridge assumptions.

lemma {:induction false} nativeOr8(x: bv8,y: bv8)
  ensures (x|y) == !(!x&!y)
{ }
lemma {:induction false} nativeXor8(x: bv8,y: bv8)
  ensures (x^y) == ((x|y)&!(x&y))
{ }

lemma {:induction false} nativeOr16(x: bv16,y: bv16)
  ensures (x|y) == !(!x&!y)
{ }
lemma {:induction false} nativeXor16(x: bv16,y: bv16)
  ensures (x^y) == ((x|y)&!(x&y))
{ }

lemma {:induction false} nativeOr32(x: bv32,y: bv32)
  ensures (x|y) == !(!x&!y)
{ }
lemma {:induction false} nativeXor32(x: bv32,y: bv32)
  ensures (x^y) == ((x|y)&!(x&y))
{ }

lemma {:induction false} nativeOr64(x: bv64,y: bv64)
  ensures (x|y) == !(!x&!y)
{ }
lemma {:induction false} nativeXor64(x: bv64,y: bv64)
  ensures (x^y) == ((x|y)&!(x&y))
{ }
