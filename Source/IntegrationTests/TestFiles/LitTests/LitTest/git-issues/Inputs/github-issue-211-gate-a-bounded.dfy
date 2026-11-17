// S.G has no bound.  An implementation in another module that is not a leaf may declare a bound of its own,
// which is checked like any other.  This one calls the library's UseS, which has no bound and dispatches to
// G, so the call is rejected: bounded code never reaches TOP.  If it were accepted, G(0) == G(0) + 1.
module Client {
  import opened Lib
  class C extends S {
    constructor() {}
    function G(n: nat): nat
      decreases {:global} n
    {
      UseS(this, n) + 1
    }
  }
  method Main() {
    var c := new C();
    assert c.G(0) == UseS(c, 0) + 1;
    assert UseS(c, 0) == (c as S).G(0);
    assert false;
  }
}
