module Zulu {
  import Alpha
  datatype T = T(n: int)
  const End: int := 4
  const Start: int := Alpha.Middle + 1
  function F(n: nat): nat { if n == 0 then 0 else 1 + Alpha.G(n - 1) }
}
