predicate P(b: Alias)
type Alias = S
type S = b: B | true
newtype B = a: A | true
newtype A = b | P(b)
