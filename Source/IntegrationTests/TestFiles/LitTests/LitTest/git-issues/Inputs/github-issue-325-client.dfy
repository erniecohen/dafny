// The client of the issue's library: F.Bad() is about F's own f, so this must not verify.
module Client {
  import F
  import G
  lemma Contra() ensures false {
    F.Bad();
    assert G.f() == 2;
  }
  lemma Fine() ensures F.f() == 1 {
    F.Bad();
  }
}
