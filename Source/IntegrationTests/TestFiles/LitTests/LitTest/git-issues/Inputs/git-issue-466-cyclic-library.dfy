module LibA {
  import LibB
  function F(n: nat): nat { if n == 0 then 0 else LibB.G(n - 1) }
}

module LibB {
  import LibA
  function G(n: nat): nat { if n == 0 then 0 else LibA.F(n - 1) }
}
