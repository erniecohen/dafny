// Source-only control; requires refresh, general-newtypes and extended-newtype-bases.
datatype Cell = Cell(value: int)
newtype Layer0 = c: Cell | 0 <= c.value witness Cell(0)
newtype Layer1 = x: Layer0 | 0 <= (x as Cell).value witness Cell(0) as Layer0
newtype Layer2 = x: Layer1 | 0 <= (x as Cell).value witness Cell(0) as Layer0 as Layer1

lemma InhabitedAdjacentChainIsNotFalse() {
  var c := Cell(0);
  var three := c as Layer0 as Layer1 as Layer2;
  var threeBase := three as Layer1 as Layer0 as Cell;
  assert threeBase == c;
  assert threeBase.value == 0;
  assert false;
}
