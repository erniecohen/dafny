type Pair = (int, int)
datatype Layer0 = Wrap0(value: Pair)
datatype Layer1 = Wrap1(value: Layer0)
datatype Layer2 = Wrap2(value: Layer1)
datatype Layer3 = Wrap3(value: Layer2)
datatype Layer4 = Wrap4(value: Layer3)
datatype Layer5 = Wrap5(value: Layer4)
datatype Layer6 = Wrap6(value: Layer5)
datatype Layer7 = Wrap7(value: Layer6)
datatype Layer8 = Wrap8(value: Layer7)
datatype Layer9 = Wrap9(value: Layer8)
lemma RoundTrip(b: Pair) { var v: Layer9 := Wrap9(Wrap8(Wrap7(Wrap6(Wrap5(Wrap4(Wrap3(Wrap2(Wrap1(Wrap0(b)))))))))); assert (v.value.value.value.value.value.value.value.value.value.value) == b; }
lemma ConcreteWitness() { RoundTrip((0, 1)); }
