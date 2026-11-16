// The same with the attribute that lets a class extend a trait of another module without the option.
module Client {
  import opened Lib
  @AssumeCrossModuleTermination class C extends S {
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
