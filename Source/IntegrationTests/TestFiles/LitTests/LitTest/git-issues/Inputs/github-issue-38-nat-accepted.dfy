greatest predicate NatFinite[nat](xs: set<int>, n: int) {
  exists m: int :: m in xs && NatFinite(xs, m)
}
least predicate NatFiniteLeast[nat](xs: set<int>, n: int) {
  forall m: int :: m in xs ==> NatFiniteLeast(xs, m)
}
