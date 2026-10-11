type Raw32 = fp32
type Raw64 = fp64
type NonNaN32 = x: Raw32 | !x.IsNaN witness 0.0
type NonNaN64 = x: Raw64 | !x.IsNaN witness 0.0
newtype Finite32 = x: NonNaN32 | x.IsFinite witness 0.0
newtype Finite64 = x: NonNaN64 | x.IsFinite witness 0.0

ghost method Invalid32() {
  var x: NonNaN32 := fp32.NaN;
}
ghost method Invalid64() {
  var x: NonNaN64 := fp64.NaN;
}
ghost method False32(x: Finite32) {
  assert false;
}
ghost method False64(x: Finite64) {
  assert false;
}

ghost method WrongSign32(x: Finite32)
  requires (x as fp32).IsZero && !(x as fp32).IsNegative
{
  var literal: Finite32 := -0.0;
  assert (literal as fp32).IsZero && (literal as fp32).IsNegative;
  var y: Finite32 := -x;
  assert !(y as fp32).IsNegative;
}

ghost method WrongSign64(x: Finite64)
  requires (x as fp64).IsZero && !(x as fp64).IsNegative
{
  var literal: Finite64 := -0.0;
  assert (literal as fp64).IsZero && (literal as fp64).IsNegative;
  var y: Finite64 := -x;
  assert !(y as fp64).IsNegative;
}
