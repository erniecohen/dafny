abstract module Base {
  datatype D = D(n: int) {
    invariant n >= 0
    witness D(0)
  }
  function Make(): D { D(0) }
}
module Left refines Base {
  import Right
  datatype D ... { invariant n > 0 }
  lemma WouldBeBad() ensures false {
    var d := Make();
    assert d.n == 0;
    assert d.n > 0;
  }
}
module Right {
  import Left
  lemma Use(d: Left.D) { assert d.n > 0; }
}

// A module outside a group may strengthen one member, but must check inherited construction.
abstract module OriginalLeft {
  import OriginalRight
  datatype D = D(n: int) {
    invariant n >= 0
    witness D(0)
  }
  function Make(): D { D(0) }
}
abstract module OriginalRight {
  import OriginalLeft
  lemma Use(d: OriginalLeft.D) { assert d.n >= 0; }
}
abstract module Stronger refines OriginalLeft {
  datatype D ... {
    invariant n > 0
  }
  lemma WouldBeBad() ensures false {
    var d := Make();
    assert d.n == 0 && d.n > 0;
  }
}
