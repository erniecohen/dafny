abstract module Base {
  function F(n: nat): nat decreases n, 0
  function G(n: nat): nat decreases n, 1 { F(n) }
  datatype Range = Range(lo: int, hi: int) {
    invariant lo <= hi
    witness Range(0, 0)
    function Width(): nat { if lo <= hi then hi - lo else 0 }
  }
  function Make(): Range { Range(0, 0) }
}
module Left refines Base {
  import Right
  function F(n: nat): nat { if n == 0 then 0 else Right.H(n - 1) }
  datatype Range ... {
    invariant hi >= 0
    invariant Width() >= 0
  }
}
module Right {
  import Left
  function H(n: nat): nat decreases n, 2 { Left.G(n) }
  lemma Use() {
    var r := Left.Make();
    assert r.lo <= r.hi && r.hi >= 0;
  }
}

module Interface { type T }
abstract module AbstractLeft {
  import AbstractRight
  import X : Interface
  datatype T = Leaf(n: int) | Node(u: AbstractRight.U) {
    invariant Leaf? ==> n >= 0
    witness Leaf(0)
  }
  function Make(): T { Leaf(0) }
}
abstract module AbstractRight {
  import AbstractLeft
  datatype U = U(t: AbstractLeft.T)
  lemma Use(t: AbstractLeft.T) { assert t.Leaf? ==> t.n >= 0; }
}
abstract module RefinedLeft refines AbstractLeft {
  datatype T ... {
    invariant Leaf? ==> n <= 10
  }
}
abstract module AbstractUser {
  import X : AbstractLeft
  lemma Use(t: X.T) { assert t.Leaf? ==> t.n >= 0; }
}
