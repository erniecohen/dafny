// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// A conversion into a newtype whose base is a datatype, tuple or codatatype
// must establish that the original value inhabits the fully instantiated base.
// Each conversion below starts from a value outside that base, so each one
// must be rejected at the conversion itself.

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

function Id(n: Bare): Bare { n }

method BareCast() {
  var x := B<int>.B(-1) as Bare; // error
}

lemma ReviewerReproducer() ensures false {
  var x := Id(B<int>.B(-1) as Bare); // error
  var y: B<nat> := x as B<nat>;
  assert y.v == -1;
}

method TrivialPredicate() {
  var x := B<int>.B(-1) as Trivial; // error
}

method NestedChain() {
  var x := B<int>.B(-1) as Outer; // error
}

method GenericActualIntToNat() {
  var x := B<int>.B(-1) as Generic<nat>; // error
}

method ParameterCast(z: B<int>) {
  var x := z as Bare; // error
}

method ThroughSubset() {
  var x := B<int>.B(-1) as ViaSubset; // error
}

method TupleCast() {
  var x := (-1, 0) as Pair; // error
}

function Negatives(): S<int> { Cons(-1, Negatives()) }

method CoDatatypeCast() {
  var x := Negatives() as Stream; // error
}

newtype IntBased = B<int> witness *

method FromOtherNewtype(m: IntBased) {
  var x := m as Bare; // error
}

method FromOtherNewtypeChecked(m: IntBased) requires (m as B<int>).v >= 0 {
  var x := m as Bare;
}

// Nonvacuity: a valid conversion does not make the rest of the body vacuous.
method ValidThenFalse() {
  var x := B<int>.B(1) as Bare;
  assert false; // error
}
