// An added invariant makes the inherited Value member raw. Its old proof used the base invariant.
abstract module Base {
  datatype D = D(n: int) {
    invariant n >= 0
    witness D(0)
    function Value(): nat { n }
  }
}
module Left refines Base {
  import Right
  datatype D ... {
    invariant Value() >= 0
  }
  lemma WouldBeBad() ensures false {
    var d: raw D := D(-1);
    assert d.Value() == -1;
    assert d.Value() >= 0;
  }
}
module Right {
  import Left
  lemma Use(d: Left.D) { assert d.Value() >= 0; }
}
