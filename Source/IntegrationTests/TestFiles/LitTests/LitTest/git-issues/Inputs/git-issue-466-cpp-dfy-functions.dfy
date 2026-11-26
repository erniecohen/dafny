module A {
  import B
  function Count(n: bv8): bv8 decreases n { if n == 0 then 0 else 1 + B.Count(n - 1) }
}
module B {
  import C
  function Count(n: bv8): bv8 decreases n { if n == 0 then 0 else 1 + C.Count(n - 1) }
}
module C {
  import A
  function Count(n: bv8): bv8 decreases n { if n == 0 then 0 else 1 + A.Count(n - 1) }
}
method Main() { print A.Count(6), " ", B.Count(7), " ", C.Count(8), "\n"; }
