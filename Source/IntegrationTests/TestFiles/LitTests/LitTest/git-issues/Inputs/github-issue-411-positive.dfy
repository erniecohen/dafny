// Fields a refining module may add to a class with an inherited constructor
// (https://github.com/erniecohen/dafny/issues/411).

module A {
  class D {
    constructor () { }
  }
  class C {
    var x: int
    constructor () ensures x == 0 { x := 0; }
    method Inc() modifies this ensures x == old(x) + 1 { x := x + 1; }
  }
  class E {
    constructor ()
  }
}

module B refines A {
  class C ... {
    var y: int          // has a default value, so no definite-assignment check
    ghost var g: nat    // ghost, of a nonempty type
    var z: D?           // nullable
  }
  class E ... {
    var d: D
    constructor () { d := new D(); }  // the refining module gives the constructor its body
  }
  method Use() {
    var c := new C();
    c.Inc();
    assert c.x == 1;
    var e := new E();
  }
}
