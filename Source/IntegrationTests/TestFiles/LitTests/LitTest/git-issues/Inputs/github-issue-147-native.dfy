// Rotations only at positive widths that are powers of two, which keep Z3's native operators.
// It only computes them: the emission check reads the BPL and the SMT, not the verdict.

method Native(x1: bv1, x32: bv32, x64: bv64, a: nat, b: nat, c: nat)
  requires a <= 1 && b <= 32 && c <= 64
{
  var y1 := x1.RotateLeft(a).RotateRight(a);
  var y32 := x32.RotateRight(b).RotateLeft(b);
  var y64 := x64.RotateLeft(c).RotateRight(c);
}
