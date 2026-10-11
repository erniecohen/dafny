// Heap successions that the guarded update axiom must keep.
class Cell {
  var v: int
  ghost var g: int
  constructor (x: int) ensures v == x && fresh(this) { v := x; }
  method Set(x: int) modifies this ensures v == x { v := x; }
}

method FieldUpdate(c: Cell) modifies c {
  c.v := 3;
  c.g := 4;
  assert c.v == 3 && c.g == 4;
  assert old(allocated(c)) && allocated(c);
}

method ArrayUpdate(a: array<int>, m: array2<int>)
  requires a.Length > 2 && m.Length0 > 1 && m.Length1 > 1
  modifies a, m
{
  a[1] := 5;
  m[1, 1] := 6;
  assert a[1] == 5 && m[1, 1] == 6;
}

method FreshAllocation(c: Cell) returns (d: Cell)
  ensures fresh(d) && d.v == 7
{
  d := new Cell(7);
  var a := new int[3];
  assert allocated(c) && allocated(d) && allocated(a);
  assert d != c;
}

class GhostCell {
  ghost var w: int
  ghost constructor (x: int) ensures w == x && fresh(this) { w := x; }
}

method GhostAllocation() returns (ghost d: GhostCell)
  ensures fresh(d) && d.w == 8
{
  d := new GhostCell(8);
  ghost var o := new object;
  assert allocated(d) && allocated(o) && d != o;
}

method Calls(c: Cell) modifies c {
  var d := new Cell(0);
  c.Set(9);
  d.Set(10);
  assert c.v == 9 && d.v == 10;
  assert old(allocated(c)) && allocated(d) && !old(allocated(d));
}

method Monotone(s: set<object>)
  requires forall o :: o in s ==> allocated(o)
{
  var c := new Cell(1);
  c.v := 2;
  label L:
  var e := new Cell(3);
  assert forall o :: o in s ==> old@L(allocated(o)) && allocated(o);
  assert old@L(allocated(c)) && allocated(c) && !old@L(allocated(e));
}

twostate lemma StaysAllocated(o: object)
  requires old(allocated(o))
  ensures allocated(o)
{}

method UsesTwoState(c: Cell) modifies c {
  c.v := 11;
  var d := new Cell(12);
  StaysAllocated(c);
  label L:
  d.v := 13;
  StaysAllocated@L(d);
}

method Framing(c: Cell, d: Cell) returns (r: int)
  requires c != d
  modifies c
  ensures d.v == old(d.v)
{
  c.v := d.v + 1;
  r := c.v;
}

iterator Gen(n: nat) yields (x: nat) {
  var i := 0;
  while i < n {
    x := i;
    yield;
    i := i + 1;
  }
}

method UseIterator() {
  var it := new Gen(3);
  var more := it.MoveNext();
  if more { assert it.xs == [it.x]; }
}
