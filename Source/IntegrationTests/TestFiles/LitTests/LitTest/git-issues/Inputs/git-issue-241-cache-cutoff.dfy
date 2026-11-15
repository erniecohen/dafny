module Api {
  function Used(x: int): int { x + 1 }
  function Unused(x: int): int { x + 10 }
  lemma Proof(x: int) ensures Used(x) == x + 1 { assert true; }
}
module ClientB {
  import A = Api
  lemma Reached(x: int) ensures A.Used(x) == x + 1 {}
  lemma Pruned() { assert true; }
}
module ClientC {
  import A = Api
  lemma Reached(x: int) ensures A.Used(x) == x + 1 {}
  lemma Pruned() { assert true; }
}
module ClientD {
  import A = Api
  lemma Reached(x: int) ensures A.Used(x) == x + 1 {}
  lemma Pruned() { assert true; }
}
module Leaf {
  function Inc(x: int): int { x + 3 }
  lemma Reached(x: int) ensures Inc(x) == x + 3 {}
}
