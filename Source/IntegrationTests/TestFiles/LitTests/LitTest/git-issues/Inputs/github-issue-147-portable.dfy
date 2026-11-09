// Rotations only at widths that use the definition by shifts (3, 6, 12) and at width 0.
// No shift expressions, which would register other widths.  It only computes the rotations:
// the emission check reads the SMT, not the verdict.

method Portable(x3: bv3, x6: bv6, x12: bv12, z: bv0, a: nat, b: nat, c: nat)
  requires a <= 3 && b <= 6 && c <= 12
{
  var y3 := x3.RotateLeft(a).RotateRight(a);
  var y6 := x6.RotateRight(b).RotateLeft(b);
  var y12 := x12.RotateLeft(c).RotateRight(c);
  var y0 := z.RotateLeft(0).RotateRight(0);
}
