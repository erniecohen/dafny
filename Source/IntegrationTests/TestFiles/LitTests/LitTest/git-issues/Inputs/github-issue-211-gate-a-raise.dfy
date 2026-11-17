// F(n) == Above(this, n) + 1 and Above(t, n) == t.F(n): if this were accepted, F(0) == F(0) + 1.
// Above has a larger bound than the slot it implements.
module Client {
  import opened Lib
  class C extends T {
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
