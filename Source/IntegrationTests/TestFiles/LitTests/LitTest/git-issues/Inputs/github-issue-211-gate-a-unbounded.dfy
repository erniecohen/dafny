// S.G has no bound, so an implementation in another module must be a leaf.  This one calls the library,
// which dispatches to it.  If it were accepted, G(0) == G(0) + 1.
module Client {
  import opened Lib
  class C extends S {
    constructor() {}
    function G(n: nat): nat
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
