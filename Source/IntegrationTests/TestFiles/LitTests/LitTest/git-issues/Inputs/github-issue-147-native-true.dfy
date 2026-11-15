// True facts about rotations at positive widths that are powers of two, which keep Z3's
// native operators, with symbolic counts.  Every method must verify.  At widths 32 and 64 the
// word is fixed too: with a symbolic word and a full-width count, Z3 5.1.0 and 4.12.1 do not
// finish some of these proofs within 200,000,000 resource units, before the fix as after it.
// The driver runs this file with a resource limit of 2,000,000.

// Width 1.
method Bv1(x: bv1, a: nat, b: nat)
  requires a <= 1 && b <= 1
  ensures x.RotateRight(a).RotateLeft(b) == x
{
}

// Widths 32 and 64.
method Bv32FullThenZero(x: bv32, a: nat, b: nat)
  requires x == (1 as bv32)
  requires a == 32 && b == 0
  ensures x.RotateRight(a).RotateLeft(b) == (1 as bv32)
{
}

method Bv32Interior(x: bv32, a: nat, b: nat)
  requires x == (1 as bv32)
  requires a == 31 && b == 1
  ensures x.RotateRight(a).RotateLeft(b) == (4 as bv32)
{
}

method Bv64SameDirection(x: bv64, a: nat)
  requires x == (1 as bv64)
  requires a == 64
  ensures x.RotateLeft(a).RotateLeft(a).RotateLeft(a) == (1 as bv64)
{
}

method Bv64Interior(x: bv64, a: nat, b: nat)
  requires x == (1 as bv64)
  requires a == 63 && b == 1
  ensures x.RotateRight(a).RotateLeft(b) == (4 as bv64)
{
}
