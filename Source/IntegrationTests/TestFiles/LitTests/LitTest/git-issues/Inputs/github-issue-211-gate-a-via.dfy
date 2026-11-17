// The same through a trait of the client that tries to raise the bound of the slot.
module Client {
  import opened Lib
  trait U extends T {
    function F(n: nat): nat
      decreases {:global} n + 1
  }
  class C extends U {
    constructor() {}
    function F(n: nat): nat
    {
      Above(this, n) + 1
    }
  }
  method Main() {
    var c := new C();
    assert c.F(0) == Above(c, 0) + 1;
    assert Above(c, 0) == (c as T).F(0);
    assert false;
  }
}
