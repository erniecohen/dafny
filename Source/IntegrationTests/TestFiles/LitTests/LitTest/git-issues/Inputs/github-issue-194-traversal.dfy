datatype W = W(value: int) {
  function {:operator "+"} plus(b: W): W { W(value + b.value) }
}
function ViaMatch(x: W, y: W): W { match x case W(v) => W(v) + y }
function ViaLambda(x: W, y: W): W { var f: W -> W := (z: W) => z + y; f(x) }
function ViaComprehension(x: W): set<W> { set y: W | y in {W(0), W(1)} :: x + y }
class Cell { var value: W }
method OldAndLabel(c: Cell, x: W)
  modifies c
{
  label L:
  c.value := x;
  assert old(c.value + x) == old(c.value).plus(x);
  assert old@L(c.value + x) == old@L(c.value).plus(x);
  assert (match x case W(v) => W(v) + x) == x.plus(x);
  assert ViaLambda(x, x) == x.plus(x);
  assert ViaComprehension(x) == {x.plus(W(0)), x.plus(W(1))};
}
