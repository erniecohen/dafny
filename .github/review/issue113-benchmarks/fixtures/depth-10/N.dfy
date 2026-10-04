type Pair = (int, int)
newtype Layer0 = Pair
newtype Layer1 = Layer0
newtype Layer2 = Layer1
newtype Layer3 = Layer2
newtype Layer4 = Layer3
newtype Layer5 = Layer4
newtype Layer6 = Layer5
newtype Layer7 = Layer6
newtype Layer8 = Layer7
newtype Layer9 = Layer8
lemma RoundTrip(b: Pair) { var v: Layer9 := ((((((((((b as Layer0) as Layer1) as Layer2) as Layer3) as Layer4) as Layer5) as Layer6) as Layer7) as Layer8) as Layer9); assert (v as Pair) == b; }
lemma ConcreteWitness() { RoundTrip((0, 1)); }
