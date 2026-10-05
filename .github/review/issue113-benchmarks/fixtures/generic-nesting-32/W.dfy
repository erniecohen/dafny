datatype Pair<T0(0), T1(0)> = P(first: T0, second: T1)
datatype Value<T0(0), T1(0)> = Wrap(value: Pair<T0, T1>)
lemma GenericRoundTrip<T0(0), T1(0)>(b: Pair<T0, T1>) {
  var v0: Value<T0, T1> := Wrap(b); assert (v0.value) == b;
  var v1: Value<T0, T1> := Wrap(b); assert (v1.value) == b;
  var v2: Value<T0, T1> := Wrap(b); assert (v2.value) == b;
  var v3: Value<T0, T1> := Wrap(b); assert (v3.value) == b;
  var v4: Value<T0, T1> := Wrap(b); assert (v4.value) == b;
  var v5: Value<T0, T1> := Wrap(b); assert (v5.value) == b;
  var v6: Value<T0, T1> := Wrap(b); assert (v6.value) == b;
  var v7: Value<T0, T1> := Wrap(b); assert (v7.value) == b;
}
lemma ConcreteWitness() { GenericRoundTrip<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<seq<int>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>, int>(P([], 0)); }
