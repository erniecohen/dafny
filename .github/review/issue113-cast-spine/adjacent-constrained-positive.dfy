// Source-only control; requires refresh, general-newtypes and extended-newtype-bases.
datatype Cell = Cell(value: int)
newtype Layer0 = c: Cell | 0 <= c.value witness Cell(0)
newtype Layer1 = x: Layer0 | 0 <= (x as Cell).value witness Cell(0) as Layer0
newtype Layer2 = x: Layer1 | 0 <= (x as Cell).value witness Cell(0) as Layer0 as Layer1

lemma AdjacentRoundTrips(c: Cell)
  requires 0 <= c.value
  ensures (c as Layer0 as Layer1 as Layer0 as Cell) == c
  ensures (c as Layer0 as Layer1 as Layer2 as Layer1 as Layer0 as Cell) == c
  ensures (c as Layer0 as Layer1 as Layer2 as Layer1 as Layer0 as Cell).value == c.value
{
  var two := c as Layer0 as Layer1;
  var twoBase := two as Layer0 as Cell;
  assert twoBase == c;
  assert twoBase.value == c.value;

  var three := c as Layer0 as Layer1 as Layer2;
  var threeBase := three as Layer1 as Layer0 as Cell;
  assert threeBase == c;
  assert threeBase.value == c.value;
}
