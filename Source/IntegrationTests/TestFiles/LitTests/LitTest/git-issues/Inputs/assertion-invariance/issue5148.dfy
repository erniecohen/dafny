class C {}

class D {
  method Foo() {}
}

datatype C' = C'(inner: C)

class E {
  var c: C'
  var d: D

  constructor()
  {
    var c_: C := new C;
    c := C'(c_);
    d := new D;
  }

  method Foo()
    ensures unchanged(c.inner)
  {
    // Uncomment to make verification succeed.
    // assert (d as object) != (c.inner as object);
    d.Foo();
    // This also works.
    // assert unchanged(c.inner);
  }
}
