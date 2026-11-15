// The definition by shifts relies on the well-formedness check 0 <= n <= W.  A negative
// count and a count above the width are still reported, at a width that uses the definition
// by shifts and at one that uses the native operators.

method NegativeBv3(x: bv3, k: int)
  requires k == -1
{
  var y := x.RotateLeft(k); // error: rotate amount must be non-negative
}

method OverWidthBv3(x: bv3, k: int)
  requires k == 4
{
  var y := x.RotateRight(k); // error: rotate amount must not exceed the width
}

method NegativeBv6(x: bv6, k: int)
  requires k == -1
{
  var y := x.RotateRight(k); // error: rotate amount must be non-negative
}

method OverWidthBv12(x: bv12, k: int)
  requires k == 13
{
  var y := x.RotateLeft(k); // error: rotate amount must not exceed the width
}

method OverWidthBv32(x: bv32, k: int)
  requires k == 33
{
  var y := x.RotateLeft(k); // error: rotate amount must not exceed the width
}
