// An implementation of the member with no bound that is a leaf: it calls nothing that can reach a dispatched
// call.
module Client {
  import opened Lib
  class C extends S {
    constructor() {}
    function G(n: nat): nat
    {
      n + 1
    }
  }
  method Main() {
    var c := new C();
    assert UseS(c, 0) == 1;
    assert false; // control: must fail
  }
}
