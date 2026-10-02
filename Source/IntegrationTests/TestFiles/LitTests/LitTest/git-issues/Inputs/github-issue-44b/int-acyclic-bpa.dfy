newtype B = x: int | 0 <= x
predicate P(b: B) { true }
newtype A = b | P(b)
newtype C = a: A | true
function Add(x: C, y: C): C { x + y }
function Multiply(x: C, y: C): C { x * y }
predicate Less(x: C, y: C) { x < y }
