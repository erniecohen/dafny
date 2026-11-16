// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// Valid controls for git-issue-113-extended-newtypes-datatype-base-cast-negative:
// the same conversions verify when the value does inhabit the instantiated base,
// and the final lemma shows the checks are not vacuous.

datatype B<T> = B(v: T)
codatatype S<T> = Cons(h: T, t: S<T>)

newtype Bare = B<nat> witness *
newtype Trivial = x: B<nat> | true witness *
newtype Inner = B<nat> witness *
newtype Outer = Inner witness *
newtype Generic<T> = B<T> witness *
type Sub = x: B<nat> | true witness *
newtype ViaSubset = Sub witness *
newtype Pair = (nat, int) witness *
newtype Stream = S<nat> witness *

method Valid(z: B<int>, s: S<nat>, n: Bare) requires z.v >= 0 {
  var a := B<int>.B(1) as Bare;
  var b := z as Bare;
  var c := B<int>.B(2) as Trivial;
  var d := B<int>.B(3) as Outer;
  var e := B<int>.B(4) as Generic<nat>;
  var f := B<int>.B(-4) as Generic<int>;
  var g := B<int>.B(5) as ViaSubset;
  var h := (1, -1) as Pair;
  var i := s as Stream;
  var j := n as Bare;
  var k := (n as B<nat>) as Outer;
  assert (a as B<nat>).v == 1;
  assert (k as Inner as B<nat>).v >= 0;
}

lemma NonVacuity(z: B<int>) requires z.v >= 0 ensures (z as Bare as B<nat>).v == z.v {
}
