type Pair = (int, int)
type Layer0 = Pair
type Layer1 = Layer0
type Layer2 = Layer1
type Layer3 = Layer2
type Layer4 = Layer3
type Layer5 = Layer4
type Layer6 = Layer5
type Layer7 = Layer6
type Layer8 = Layer7
type Layer9 = Layer8
lemma RoundTrip(b: Pair) { var v: Layer9 := b; assert (v) == b; }
lemma ConcreteWitness() { RoundTrip((0, 1)); }
