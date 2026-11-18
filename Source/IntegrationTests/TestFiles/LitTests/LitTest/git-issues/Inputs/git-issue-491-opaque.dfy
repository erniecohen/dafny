// A provided predicate is mixed even when its hidden body has a single polarity.
module Library {
  export provides All, Make, Contradiction reveals Empty
  type Empty = x: int | false witness *
  ghost predicate All<X(!new)>() { forall y: X {:nowarn} :: false }
  lemma Make() ensures All<Empty>() {}
  lemma Contradiction() requires All<int>() ensures false {}
}
module Client {
  import opened Library
  datatype Box<+X(!new)> = Box(b: bool)
  type S<+X(!new)> = s: Box<X> | s.b ==> All<X>() witness *
  lemma Use(t: S<int>) requires t.b ensures false { Contradiction(); }
  lemma FalseProof() {
    Make();
    var s: S<Empty> := Box(true);
    var t: S<int> := s;
    Use(t);
    assert false;
  }
}
