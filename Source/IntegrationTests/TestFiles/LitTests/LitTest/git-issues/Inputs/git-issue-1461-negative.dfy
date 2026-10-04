// Vacuity controls: a two-state function is used as a value in each method, and none may verify.
class C {
  var x: int

  twostate predicate Unchanged() reads this { x == old(x) }

  method Vacuity() {
    var q := Unchanged;
    assert q();
    assert false;
  }

  method AfterUpdate() modifies this {
    var q := Unchanged;
    x := x + 1;
    assert q();
  }

  method Labeled() modifies this {
    label L:
    x := x + 1;
    assert Unchanged@L();
  }

  var next: C?

  // Its precondition and reads frame depend on the heap.
  twostate function Next(): int
    requires next != null ==> old(x) <= x
    reads this, if next != null then {next} else {}
  {
    if next != null then next.x else 0
  }

  // The allocation of a value whose precondition and reads frame depend on the heap, with the instance in
  // play, and then a new object and a write that changes both.
  method ArrowAllocation() modifies this {
    var q := Next;
    assert allocated(q) && q.requires();
    var e := new C;
    next := e;
    assert false;
  }
}
