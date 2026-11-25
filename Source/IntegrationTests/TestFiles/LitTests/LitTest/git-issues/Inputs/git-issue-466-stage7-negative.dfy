module A {
  import B
  datatype Positive = Positive(n: int) {
    invariant n > 0
    witness Positive(2)
  }
  function Count(n: nat): nat { if n == 0 then 0 else 1 + B.Count(n - 1) }
}
module B {
  import A
  function Count(n: nat): nat { if n == 0 then 0 else 1 + A.Count(n - 1) }
  method Main() {
    var p: A.Positive := A.Positive(0); // The invariant must still be checked before compilation.
  }
  lemma False() {
    assert A.Count(0) == 0;
    assert false;
  }
}
