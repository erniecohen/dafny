// Vacuity and reachability controls for issue 147.  Each assert false must FAIL: the
// preconditions are satisfiable, and the branch on the rotation's correct value is reachable.

method AssertFalseUnderPreconditions(x: bv3, a: nat, b: nat)
  requires x == (1 as bv3)
  requires a == 3 && b == 0
{
  assert false; // error: the context is consistent
}

method RotationReachable(x: bv3, a: nat, b: nat)
  requires x == (1 as bv3)
  requires a == 3 && b == 0
{
  if x.RotateRight(a).RotateLeft(b) == (1 as bv3) {
    assert false; // error: this branch is reachable
  }
}

method RotationReachableOtherOrder(x: bv3, a: nat, b: nat)
  requires x == (1 as bv3)
  requires a == 3 && b == 0
{
  if x.RotateLeft(a).RotateRight(b) == (1 as bv3) {
    assert false; // error: this branch is reachable
  }
}

method RotationReachableBv6(x: bv6, a: nat, b: nat)
  requires x == (1 as bv6)
  requires a == 5 && b == 1
{
  if x.RotateRight(a).RotateLeft(b) == (4 as bv6) {
    assert false; // error: this branch is reachable
  }
}
