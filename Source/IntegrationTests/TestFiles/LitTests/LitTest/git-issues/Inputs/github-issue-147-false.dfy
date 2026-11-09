// False companions of github-issue-147-true.dfy.  Each postcondition is the value that
// composing rotation counts modulo 2^W, instead of modulo W, would give (for widths that
// are powers of two, where that composition is sound, a plainly wrong value).  Every
// method must report its postcondition as unproved.

method Bv3OppositeOrderBad(x: bv3, a: nat, b: nat)
  requires x == (1 as bv3)
  requires a == 3 && b == 0
  ensures x.RotateLeft(a).RotateRight(b) == (2 as bv3) // error
{
}

method Bv3InteriorBad(x: bv3, a: nat, b: nat)
  requires x == (1 as bv3)
  requires a == 2 && b == 1
  ensures x.RotateRight(a).RotateLeft(b) == (2 as bv3) // error
{
}

method Bv3SameDirectionThreeByThreeBad(x: bv3, a: nat)
  requires x == (1 as bv3)
  requires a == 3
  ensures x.RotateLeft(a).RotateLeft(a).RotateLeft(a) == (2 as bv3) // error
{
}

method Bv3SameDirectionFourByTwoBad(x: bv3, a: nat)
  requires x == (1 as bv3)
  requires a == 2
  ensures x.RotateLeft(a).RotateLeft(a).RotateLeft(a).RotateLeft(a) == (1 as bv3) // error
{
}

method Bv6FullThenZeroBad(x: bv6, a: nat, b: nat)
  requires x == (1 as bv6)
  requires a == 6 && b == 0
  ensures x.RotateRight(a).RotateLeft(b) == (16 as bv6) // error
{
}

method Bv6InteriorBad(x: bv6, a: nat, b: nat)
  requires x == (1 as bv6)
  requires a == 5 && b == 1
  ensures x.RotateRight(a).RotateLeft(b) == (1 as bv6) // error
{
}

method Bv12FullThenZeroBad(x: bv12, a: nat, b: nat)
  requires x == (1 as bv12)
  requires a == 12 && b == 0
  ensures x.RotateRight(a).RotateLeft(b) == (16 as bv12) // error
{
}

method Bv12InteriorBad(x: bv12, a: nat, b: nat)
  requires x == (1 as bv12)
  requires a == 11 && b == 1
  ensures x.RotateRight(a).RotateLeft(b) == (64 as bv12) // error
{
}

method Bv32InteriorBad(x: bv32, a: nat, b: nat)
  requires x == (1 as bv32)
  requires a == 31 && b == 1
  ensures x.RotateRight(a).RotateLeft(b) == (2 as bv32) // error
{
}

method Bv64InteriorBad(x: bv64, a: nat, b: nat)
  requires x == (1 as bv64)
  requires a == 63 && b == 1
  ensures x.RotateRight(a).RotateLeft(b) == (2 as bv64) // error
{
}
