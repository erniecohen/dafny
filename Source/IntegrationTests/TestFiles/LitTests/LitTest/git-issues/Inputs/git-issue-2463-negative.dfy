// The heap facts stay consistent: none of these may verify.
class Cell {
  var v: int
  constructor (x: int) ensures v == x && fresh(this) { v := x; }
}

method AfterAllocation() {
  var c := new Cell(1);
  c.v := 2;
  assert false;
}

method FreshWasAllocated() {
  var c := new Cell(1);
  assert old(allocated(c));
}

method NotAllocated(c: Cell) modifies c {
  c.v := 3;
  assert !allocated(c);
}

twostate lemma AllocatedWasOld(new o: object)
  ensures old(allocated(o))
{}
