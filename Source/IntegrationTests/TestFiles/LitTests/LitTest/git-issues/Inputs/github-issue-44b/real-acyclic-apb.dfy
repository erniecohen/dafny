newtype A = b | P(b)
predicate P(b: B) { true }
newtype B = x: real | 0.0 <= x
newtype C = a: A | true
function Add(x: C, y: C): C { x + y }
function Multiply(x: C, y: C): C { x * y }
predicate Less(x: C, y: C) { x < y }
