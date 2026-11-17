// Calls that cannot change state known to the parent module remain allowed in statements a skeleton adds
// (https://github.com/erniecohen/dafny/issues/412).

module A {
  class C {
    var f: int
    constructor (x: int) ensures f == x { f := x; }
  }
  lemma L(c: C) ensures c.f == c.f {}
  method Get(c: C) returns (r: int) ensures r == c.f { r := c.f; }
  method Pure(x: int) returns (r: int) ensures r == x + 1 { r := x + 1; }
  method M(c: C) modifies c ensures c.f == 1 { c.f := 1; }
}

module B refines A {
  method NewHere(x: int) returns (r: int) ensures r == x { r := x; }
  method M ... ensures c.f == 1 {
    var a := Pure(3);
    ...;
    L(c);
    var g := Get(c);
    assert g == 1;
    var b := NewHere(a);
    var d := new C(b);  // a constructor that modifies only the new object
    assert d.f == 4 && c.f == 1;
  }
}
