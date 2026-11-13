module Client {
  import L = Leaf

  lemma Good(p: L.Positive) {
    assert p as int > 0;
    assert (1 as L.Positive).Value() == 1;
    assert (2 as L.Plain).Value() == 2;
    assert L.D(3).Value() == 3;
    assert L.C(4).Value() == 4;
  }

  lemma Bad() {
    var p := 0 as L.Positive;
  }
}
