module Zulu {
  import Alpha
  import Seed
  datatype T = T(n: int)
  class Item extends Seed.Value {
    constructor () { }
    function Get(): int { 4 }
  }
  const End: int := Seed.End
  const Start: int := Alpha.Middle + 1
  function F(n: nat): nat { if n == 0 then 0 else 1 + Alpha.G(n - 1) }
  function Make(): Alpha.T { Alpha.T(T(7)) }
}

module Seed {
  trait Value { function Get(): int }
  const End: int := 4
}
