module B {
  import X = A
  lemma Good(x: int) ensures X.Value()(x) == x + 1 {}
}
