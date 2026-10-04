type Pair = (int, int)
datatype Layer0 = Wrap0(value: Pair)
lemma RoundTrip(b: Pair) { var v: Layer0 := Wrap0(b); assert (v.value) == b; }
lemma ConcreteWitness() { RoundTrip((0, 1)); }
