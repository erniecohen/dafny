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

twostate lemma PreviousHeap(c: Cell) {
  var f := (x: int) requires x > old(c.n) => x;
  assert f.requires(old(c.n));
}

method LabelledHeap(c: Cell)
  modifies c
{
  var n := c.n;
  label Here:
  var f := (x: int) requires x > old@Here(c.n) => x;
  c.n := n + 10;
  assert f.requires(n);
}

type Empty = x: int | false witness *

lemma EmptyFormalVacuity() {
  var f := (x: Empty) requires true => 0;
  assert false;
}

predicate Partial(x: int)
  requires x != 0
{ true }

lemma MissingRangePermission() {
  var f := (x: int) requires x != 0 && Partial(x) => x;
  assert f.requires(1);
  assert Partial(0);
}

function Generic<T(==)>(value: T): T --> T {
  (x: T) requires x == value => x
}

lemma GenericVacuity() {
  var f := Generic(0);
  assert f.requires(0);
  assert false;
}

lemma NestedVacuity() {
  var outer := (x: int) => (y: int) requires y > x => y;
  var f := outer(0);
  assert f.requires(1);
  assert false;
}

function Captured(c: Cell): int ~> int {
  (x: int) requires x > c.n reads c => x
}

method CapturedLifetimeVacuity() {
  label Before:
  var c := new Cell;
  var f := Captured(c);
  assert !old@Before(allocated(c));
  assert f.requires(c.n + 1);
  assert false;
}
