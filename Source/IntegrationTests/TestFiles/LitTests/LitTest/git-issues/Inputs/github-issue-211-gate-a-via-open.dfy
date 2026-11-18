// The same through an open trait of the client that declares no bound of its own: the slot keeps the
// bound the library declared.
module Client {
  import opened Lib
  trait {:termination false} U extends T {
    function F(n: nat): nat
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
