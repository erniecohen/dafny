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
}
