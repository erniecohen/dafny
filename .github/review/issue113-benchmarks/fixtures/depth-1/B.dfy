type Pair = (int, int)
type Layer0 = Pair
lemma RoundTrip(b: Pair) { var v: Layer0 := b; assert (v) == b; }
lemma ConcreteWitness() { RoundTrip((0, 1)); }
