// The same from a module that refines the library module, where the class would be in the module of the
// trait.
module Ext refines Lib {
  class B extends S {
    constructor() {}
    function G(n: nat): nat
    {
      UseS(this, n) + 1
    }
  }
  method Main() {
    var c := new B();
    assert c.G(0) == UseS(c, 0) + 1;
    assert UseS(c, 0) == (c as S).G(0);
    assert false;
  }
}
