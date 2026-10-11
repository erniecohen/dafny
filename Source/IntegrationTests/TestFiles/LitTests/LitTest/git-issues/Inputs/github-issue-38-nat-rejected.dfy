// The nat rule requires finite branching even for ordinal-safe types.
greatest predicate NatGreatest[nat](n: int) {
  exists m: int :: m < n && NatGreatest(m)
}
least predicate NatLeast[nat](n: int) {
  forall m: int :: m < n ==> NatLeast(m)
}
