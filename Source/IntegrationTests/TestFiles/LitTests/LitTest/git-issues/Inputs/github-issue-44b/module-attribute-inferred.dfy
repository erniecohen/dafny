module {:foo P(0)} M {
  newtype A = b | P(b)
  newtype B = a: A | true
  predicate P(b: B) { true }
}
