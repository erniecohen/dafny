// Uses the stored library: its user-written {:auto_generated} members are there, and the
// generated reveal lemmas are generated again.  Only False fails.
module LibClient {
  import Lib
  import Base
  lemma Facts(k: Lib.K, d: Lib.D) ensures Lib.f() == 10 && Lib.c == 10 && Lib.P(10) && k.h() == 3 && d.h() == 5 {
    Lib.L();
    Lib.UsesF();
  }
  lemma Reveals(k: Lib.K) ensures Lib.g() == 7 && k.o() == 4 {
    Lib.reveal_g();
    k.reveal_o();
  }
  method Call() {
    var r := Lib.M();
    assert r == 10;
  }
  lemma False() ensures false {
    Lib.UsesF();
    assert Base.f() == 20;
  }
}
