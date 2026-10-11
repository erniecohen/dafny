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
