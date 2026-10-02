newtype A = b | P(b)
type Alias = B
newtype B = a: A | true
predicate P(b: Alias)
