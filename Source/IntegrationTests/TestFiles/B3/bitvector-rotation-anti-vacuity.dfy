lemma RotationFalse(x: bv3,a: nat,b: nat)
  requires x == (1 as bv3)
  requires a == 3 && b == 0
{
  var y := x.RotateRight(a).RotateLeft(b);
  assert false;
}
