datatype W = W(value: int) {
  function {:operator "+"} plus(b: W): W { W(value + b.value) }
}
function Nested(x: W, y: W): W { (x + y) + x }
function Late(x: W, y: W): W { var z := x; z + y }
function Lambda(): W -> W { (x: W) => x + x }
function Order1(x: W, y: W): W { var z := x; var r := z + y; r }
function Order2(x: W, y: W): W { var z := x; var q := y; z + q }
lemma Check(x: W, y: W) {
  assert Nested(x, y) == x.plus(y).plus(x);
  assert Late(x, y) == x.plus(y);
  assert Lambda()(x) == x.plus(x);
  assert Order1(x, y) == Order2(x, y);
}
