// Source-only control; requires refresh, general-newtypes and extended-newtype-bases.
datatype Cell = Cell(value: int)
newtype Layer0 = c: Cell | 0 <= c.value witness Cell(0)
newtype Layer1 = x: Layer0 | 0 < (x as Cell).value witness Cell(1) as Layer0
newtype Layer2 = x: Layer1 | true witness Cell(1) as Layer0 as Layer1

lemma BadIntermediateCannotBeErased() {
  var layer0 := Cell(0) as Layer0;
  var outer := layer0 as Layer1 as Layer2;
}
