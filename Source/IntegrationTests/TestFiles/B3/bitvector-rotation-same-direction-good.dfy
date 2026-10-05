method SameDirectionGood(x: bv3, a: nat, b: nat)
  requires x == (1 as bv3)
  requires a == 3 && b == 3
  ensures x.RotateLeft(a).RotateLeft(b) == (1 as bv3)
{
}

