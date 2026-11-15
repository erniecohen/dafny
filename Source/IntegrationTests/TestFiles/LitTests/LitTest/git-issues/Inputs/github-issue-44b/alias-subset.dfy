newtype A = b | P(b)
type Alias = S
type S = b: B | true
newtype B = a: A | true
predicate P(b: Alias)
