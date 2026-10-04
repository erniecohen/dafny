type Pair = (int, int)
newtype Layer0 = Pair
lemma RoundTrip(b: Pair) { var v: Layer0 := (b as Layer0); assert (v as Pair) == b; }
lemma ConcreteWitness() { RoundTrip((0, 1)); }
