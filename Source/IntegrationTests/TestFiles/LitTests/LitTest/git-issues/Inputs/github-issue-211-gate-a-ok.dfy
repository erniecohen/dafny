// An extension within the rules: F is implemented by a call of the library under the same bound.
module Client {
  import opened Lib
  class C extends T {
    constructor() {}
    function F(n: nat): nat
    {
      Sum(this, n)
    }
  }
  method Main() {
    var c := new C();
    assert c.F(0) == 0;
    assert false; // control: must fail
  }
}
