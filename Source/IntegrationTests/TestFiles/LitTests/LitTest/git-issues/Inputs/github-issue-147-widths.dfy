// One rotation in each direction at each width named in the plan for issue 147, for
// inspecting the emitted declarations: 0, 1, 3, 6, 12, 32 and 65.

method Widths(x0: bv0, x1: bv1, x3: bv3, x6: bv6, x12: bv12, x32: bv32, x65: bv65, n: nat)
  requires n <= 1
{
  var y0 := x0.RotateLeft(0).RotateRight(0);
  var y1 := x1.RotateLeft(n).RotateRight(n);
  var y3 := x3.RotateLeft(n).RotateRight(n);
  var y6 := x6.RotateLeft(n).RotateRight(n);
  var y12 := x12.RotateLeft(n).RotateRight(n);
  var y32 := x32.RotateLeft(n).RotateRight(n);
  var y65 := x65.RotateLeft(n).RotateRight(n);
}
