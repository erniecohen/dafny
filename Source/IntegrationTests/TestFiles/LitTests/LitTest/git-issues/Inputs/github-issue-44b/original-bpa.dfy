newtype B = a: A | true
predicate P(b: B)
newtype A = b | P(b)
