// The attribute's result has declared type B, so it imposes no int-to-B constraint.
module {:foo F()} M {
  newtype A = b | P(b)
  newtype B = a: A | true
  predicate P(b: B) { true }
  function F(): B
}
