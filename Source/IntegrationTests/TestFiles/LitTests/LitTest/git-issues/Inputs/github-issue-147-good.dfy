// The true counterpart of github-issue-147-original.dfy: rotating three bits right by three
// and then left by zero is the identity.
method MixedGood(x: bv3, a: nat, b: nat)
  requires x == (1 as bv3)
  requires a == 3 && b == 0
  ensures x.RotateRight(a).RotateLeft(b) == (1 as bv3)
{
}
