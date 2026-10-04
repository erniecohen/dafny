datatype Pair<T0(0), T1(0)> = P(first: T0, second: T1)
newtype Value<T0(0), T1(0)> = Pair<T0, T1>
lemma GenericRoundTrip<T0(0), T1(0)>(b: Pair<T0, T1>) {
  var v0: Value<T0, T1> := b as Value<T0, T1>; assert (v0 as Pair<T0, T1>) == b;
  var v1: Value<T0, T1> := b as Value<T0, T1>; assert (v1 as Pair<T0, T1>) == b;
  var v2: Value<T0, T1> := b as Value<T0, T1>; assert (v2 as Pair<T0, T1>) == b;
  var v3: Value<T0, T1> := b as Value<T0, T1>; assert (v3 as Pair<T0, T1>) == b;
  var v4: Value<T0, T1> := b as Value<T0, T1>; assert (v4 as Pair<T0, T1>) == b;
  var v5: Value<T0, T1> := b as Value<T0, T1>; assert (v5 as Pair<T0, T1>) == b;
  var v6: Value<T0, T1> := b as Value<T0, T1>; assert (v6 as Pair<T0, T1>) == b;
  var v7: Value<T0, T1> := b as Value<T0, T1>; assert (v7 as Pair<T0, T1>) == b;
}
lemma ConcreteWitness() { GenericRoundTrip<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<int>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>, int>(P([], 0)); }
