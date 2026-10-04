// Two-state functions used as values where the previous heap is the current one.
class C {
  var x: int

  twostate predicate Unchanged() reads this { x == old(x) }
  twostate function Delta(): int reads this { x - old(x) }

  method AtEntry() {
    var q := Unchanged;
    assert q();
    var d := Delta;
    assert d() == 0;
  }

  method AfterUpdate() modifies this {
    x := x + 1;
    var d := Delta;
    assert d() == 1;
  }

  method Passed() {
    var q := Unchanged;
    assert q.requires() && Apply(q);
  }
}

function Apply(f: () ~> bool): bool
  requires f.requires()
  reads f.reads()
{
  f()
}

twostate lemma Same(c: C)
  ensures c.Unchanged() == (c.x == old(c.x))
{}

method CallAtEntry(c: C) {
  Same(c);
  assert c.Unchanged();
}
