module IntegerChain {
  newtype A = a: int | true
  type Alias = A
  type S = a: Alias | true
  newtype B = b: S | true
  newtype C = c: B | true
  function Arithmetic(x: C, y: C): C { x + y * x - y }
  function Division(x: C, y: C): C { x / y }
  function Modulo(x: C, y: C): C { x % y }
}
module RealChain {
  newtype A = a: real | true
  type Alias = A
  type S = a: Alias | true
  newtype B = b: S | true
  newtype C = c: B | true
  function Arithmetic(x: C, y: C): C { x + y * x - y }
  function Division(x: C, y: C): C { x / y }
}
