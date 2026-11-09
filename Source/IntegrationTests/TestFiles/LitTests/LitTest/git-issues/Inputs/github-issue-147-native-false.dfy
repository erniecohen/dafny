// False companions of github-issue-147-native-true.dfy, at widths 1, 32 and 64.  Composing
// counts modulo 2^W is sound at these widths, so each value here is plainly wrong.  Every
// method must report its postcondition as unproved.

method Bv1Bad(x: bv1, a: nat, b: nat)
  requires a == 1 && b == 0
  ensures x.RotateRight(a).RotateLeft(b) != x // error
{
}

method Bv32FullThenZeroBad(x: bv32, a: nat, b: nat)
  requires a == 32 && b == 0
  ensures x.RotateRight(a).RotateLeft(b) != x // error
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

method Bv64SameDirectionBad(x: bv64, a: nat)
  requires a == 64
  ensures x.RotateLeft(a).RotateLeft(a).RotateLeft(a) != x // error
{
}
