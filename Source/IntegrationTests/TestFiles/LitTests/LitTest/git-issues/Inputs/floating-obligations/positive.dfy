type Raw32 = fp32
type Raw64 = fp64
type NonNaN32 = x: Raw32 | !x.IsNaN witness 0.0
type NonNaN64 = x: Raw64 | !x.IsNaN witness 0.0
newtype Finite32 = x: NonNaN32 | x.IsFinite witness 0.0
newtype Finite64 = x: NonNaN64 | x.IsFinite witness 0.0

ghost method Inhabited32() {
  var x: Finite32 := 0.0;
  assert x.IsFinite && !x.IsNaN;
}
ghost method Inhabited64() {
  var x: Finite64 := 0.0;
  assert x.IsFinite && !x.IsNaN;
}
ghost method Preserve32(x: Finite32) returns (y: NonNaN32)
  ensures !y.IsNaN
{
  y := x as NonNaN32;
}
ghost method Preserve64(x: Finite64) returns (y: NonNaN64)
  ensures !y.IsNaN
{
  y := x as NonNaN64;
}
