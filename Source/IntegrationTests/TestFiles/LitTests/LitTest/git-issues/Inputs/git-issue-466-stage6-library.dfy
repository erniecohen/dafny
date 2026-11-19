module A {
  import B
  datatype T = T(n: int) {
    invariant B.Good(n)
    witness T(0)
  }
  function Count(n: nat): nat { if n == 0 then 0 else B.Count(n - 1) }
}
module B {
  import A
  predicate Good(n: int) { n >= 0 }
  function Count(n: nat): nat { if n == 0 then 0 else A.Count(n - 1) }
}
