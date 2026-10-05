datatype Pair<T0(0), T1(0)> = P(first: T0, second: T1)
type Value<T0(0), T1(0)> = Pair<T0, T1>
lemma GenericRoundTrip<T0(0), T1(0)>(b: Pair<T0, T1>) {
  var v0: Value<T0, T1> := b; assert (v0) == b;
  var v1: Value<T0, T1> := b; assert (v1) == b;
  var v2: Value<T0, T1> := b; assert (v2) == b;
  var v3: Value<T0, T1> := b; assert (v3) == b;
  var v4: Value<T0, T1> := b; assert (v4) == b;
  var v5: Value<T0, T1> := b; assert (v5) == b;
  var v6: Value<T0, T1> := b; assert (v6) == b;
  var v7: Value<T0, T1> := b; assert (v7) == b;
}
lemma ConcreteWitness() { GenericRoundTrip<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<int>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>, int>(P([], 0)); }
