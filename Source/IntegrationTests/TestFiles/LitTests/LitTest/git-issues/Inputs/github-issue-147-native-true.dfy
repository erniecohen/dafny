// True facts about rotations at positive widths that are powers of two, which keep Z3's
// native operators, with symbolic counts.  Every method must verify.  The native operators
// on symbolic counts at widths 32 and 64 need more than the issue's resource limit of 200,000,
// with or without the fix, so the driver runs this file with a larger one.
// Width 1, a power of two: the native operators.
method Bv1(x: bv1, a: nat, b: nat)
  requires a <= 1 && b <= 1
  ensures x.RotateRight(a).RotateLeft(b) == x
{
}

// Widths 32 and 64, powers of two: the native operators.
method Bv32FullThenZero(x: bv32, a: nat, b: nat)
  requires a == 32 && b == 0
  ensures x.RotateRight(a).RotateLeft(b) == x
{
}

method Bv32Interior(x: bv32, a: nat, b: nat)
  requires x == (1 as bv32)
  requires a == 31 && b == 1
  ensures x.RotateRight(a).RotateLeft(b) == (4 as bv32)
{
}

method Bv32Inverse(x: bv32, a: nat)
  requires a <= 32
  ensures x.RotateLeft(a).RotateRight(a) == x
{
}

method Bv64SameDirection(x: bv64, a: nat)
  requires a == 64
  ensures x.RotateLeft(a).RotateLeft(a).RotateLeft(a) == x
{
}

method Bv64Interior(x: bv64, a: nat, b: nat)
  requires x == (1 as bv64)
  requires a == 63 && b == 1
  ensures x.RotateRight(a).RotateLeft(b) == (4 as bv64)
{
}
