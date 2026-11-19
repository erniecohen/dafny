module Zulu {
  import Alpha
  import Seed
  datatype T = T(n: int)
  const End: int := Seed.End
  const Start: int := Alpha.Middle + 1
  function F(n: nat): nat { if n == 0 then 0 else 1 + Alpha.G(n - 1) }
}

module Seed { const End: int := 4 }
