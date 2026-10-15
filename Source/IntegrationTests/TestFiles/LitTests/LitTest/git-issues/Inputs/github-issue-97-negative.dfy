lemma OutsideRange() {
  var f := (x: int) requires x > 0 => x;
  assert f(0) == 0;
}

lemma FalseRange() {
  var f := (x: int) requires false => x;
  assert f.requires(0);
}

lemma Vacuity() {
  var f := (x: int) requires x > 0 => x;
  assert f.requires(1);
  assert false;
}

lemma DistinctCaptures(offset: int) {
  var f := (x: int) requires x > offset => x;
  var g := (x: int) requires x > offset + 1 => x;
  assert f.requires(offset + 1);
  assert g.requires(offset + 1);
}

class Cell {
  var n: int
}

method ChangedHeap(c: Cell)
  modifies c
{
  c.n := 0;
  var f := (x: int) requires x > c.n reads c => x;
  assert f.requires(1);
  c.n := 1;
  assert f.requires(1);
}

twostate lemma PreviousHeap(old c: Cell) {
  var f := (x: int) requires x > old(c.n) => x;
  assert f.requires(old(c.n));
}
