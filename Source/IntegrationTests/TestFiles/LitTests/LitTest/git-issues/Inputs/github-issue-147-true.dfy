// True facts about rotations, with symbolic counts fixed by preconditions, as in issue 147.
// Z3 rewrites rotations by literal counts before it composes them, so the counts stay
// parameters.  Every method must verify.

// Width 3, not a power of two.
method Bv3OppositeOrder(x: bv3, a: nat, b: nat)
  requires x == (1 as bv3)
  requires a == 3 && b == 0
  ensures x.RotateLeft(a).RotateRight(b) == (1 as bv3)
{
}

method Bv3Interior(x: bv3, a: nat, b: nat)
  requires x == (1 as bv3)
  requires a == 2 && b == 1
  ensures x.RotateRight(a).RotateLeft(b) == (4 as bv3)
{
}

method Bv3SameDirectionThreeByThree(x: bv3, a: nat)
  requires x == (1 as bv3)
  requires a == 3
  ensures x.RotateLeft(a).RotateLeft(a).RotateLeft(a) == (1 as bv3)
{
}

method Bv3SameDirectionFourByTwo(x: bv3, a: nat)
  requires x == (1 as bv3)
  requires a == 2
  ensures x.RotateLeft(a).RotateLeft(a).RotateLeft(a).RotateLeft(a) == (4 as bv3)
{
}

method Bv3Endpoints(x: bv3, a: nat)
  requires a == 0 || a == 3
  ensures x.RotateLeft(a) == x && x.RotateRight(a) == x
{
}

method Bv3Inverse(x: bv3, a: nat)
  requires a <= 3
  ensures x.RotateLeft(a).RotateRight(a) == x
{
}

// Width 6, even but not a power of two.
method Bv6FullThenZero(x: bv6, a: nat, b: nat)
  requires x == (1 as bv6)
  requires a == 6 && b == 0
  ensures x.RotateRight(a).RotateLeft(b) == (1 as bv6)
{
}

method Bv6Interior(x: bv6, a: nat, b: nat)
  requires x == (1 as bv6)
  requires a == 5 && b == 1
  ensures x.RotateRight(a).RotateLeft(b) == (4 as bv6)
{
}

method Bv6SameDirection(x: bv6, a: nat)
  requires x == (1 as bv6)
  requires a == 5
  ensures x.RotateLeft(a).RotateLeft(a).RotateLeft(a) == (8 as bv6)
{
}

// Width 12, even but not a power of two.
method Bv12FullThenZero(x: bv12, a: nat, b: nat)
  requires x == (1 as bv12)
  requires a == 12 && b == 0
  ensures x.RotateRight(a).RotateLeft(b) == (1 as bv12)
{
}

method Bv12Interior(x: bv12, a: nat, b: nat)
  requires x == (1 as bv12)
  requires a == 11 && b == 1
  ensures x.RotateRight(a).RotateLeft(b) == (4 as bv12)
{
}

// Width 0.
method Bv0(x: bv0, a: nat)
  requires a == 0
  ensures x.RotateLeft(a) == x && x.RotateRight(a).RotateLeft(a) == x
{
}

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
