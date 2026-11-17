// Knots through the heap and the parameter and result types of a bounded value's function.  Only the top-level
// arrow of the function type is exempt from the rule against storing heap-reading functions: a bounded function
// that hands out a raw heap-reading function would let code without a bound apply it.  Before the fix, Field
// verified "ensures false".
module Field {
  class Ref {
    ghost var f: bounded<nat, () -> (() ~> bool)>
    constructor (f: bounded<nat, () -> (() ~> bool)>) ensures this.f == f { this.f := f; } // error: () ~> bool in the result
  }
  method Main() ensures false {
    var r := new Ref(bounded.certify(0, () => (() => true)));
    ghost var knot := () reads * => if r.f().requires() then !r.f()() else false;
    r.f := bounded.certify(0, () => knot); // error: () ~> bool in the result
    assert r.f.erase() == knot;
    assert knot.requires();
    assert knot() == !knot();
  }
}
// The same through a generic holder.
module Generic {
  class Holder<T> {
    ghost var f: T
    constructor (ghost f: T) ensures this.f == f { this.f := f; }
  }
  method Main() ensures false {
    var r := new Holder<bounded<nat, () -> (() ~> bool)>>(bounded.certify(0, () => (() => true)));
    ghost var knot := () reads * => if r.f().requires() then !r.f()() else false;
    r.f := bounded.certify(0, () => knot); // error: () ~> bool in the result
    assert r.f.erase() == knot;
    assert knot() == !knot();
  }
}
// The same through a sequence field.
module Sequence {
  class R {
    ghost var fs: seq<bounded<nat, () -> (() ~> bool)>>
    constructor () ensures fs == [] { fs := []; } // error: () ~> bool in the result
  }
  method Main() ensures false {
    var r := new R();
    ghost var knot := () reads * => if |r.fs| == 1 && r.fs[0]().requires() then !r.fs[0]()() else false;
    r.fs := [bounded.certify(0, () => knot)]; // error: () ~> bool in the result
    assert r.fs[0].erase() == knot;
    assert knot() == !knot();
  }
}
// The same through an array element.
module Array {
  method Main() ensures false {
    var a := new bounded<nat, () -> (() ~> bool)>[1](i => bounded.certify(0, () => (() => true)));
    var knot := () reads * => !a[0]()();
    a[0] := bounded.certify(0, () => knot); // error: () ~> bool in the result
    assert a[0].erase() == knot;
    assert knot() == !knot();
  }
}
// A raw heap-reading function as a parameter of the bounded function.
module Parameter {
  class R {
    var f: bounded<nat, (() ~> bool) -> int>
    constructor (f: bounded<nat, (() ~> bool) -> int>) { this.f := f; } // error: () ~> bool in a parameter
  }
}
// A raw heap-reading function inside a datatype in the result of the bounded function (issue 479).
module DatatypeResult {
  datatype Box = Box(g: () ~> bool)
  class Ref {
    ghost var f: bounded<nat, () -> Box>
    constructor (f: bounded<nat, () -> Box>) ensures this.f == f { this.f := f; } // error: Box holds () ~> bool
  }
  method Main() ensures false {
    var r := new Ref(bounded.certify(0, () => Box(() => true)));
    ghost var knot := () reads * => if r.f().g.requires() then !r.f().g() else false;
    r.f := bounded.certify(0, () => Box(knot)); // error: Box holds () ~> bool
    assert r.f.erase() == Box(knot);
    assert knot.requires();
    assert knot() == !knot();
  }
}
// The same inside a datatype parameter of the bounded function.
module DatatypeParameter {
  datatype Box = Box(g: () ~> bool)
  class R {
    var f: bounded<nat, Box -> int>
    constructor () { f := bounded.certify(0, (b: Box) => 0); } // error: Box holds () ~> bool
  }
}
// The same through a function type that is a subset type, whose result is a heap-reading function only in its
// definition.
module SubsetFunction {
  type Fn = f: () -> (() ~> bool) | true
  class Ref {
    ghost var f: bounded<nat, Fn>
    constructor (f: bounded<nat, Fn>) ensures this.f == f { this.f := f; } // error: () ~> bool in the result
  }
  method Main() ensures false {
    var r := new Ref(bounded.certify(0, () => (() => true)));
    ghost var knot := () reads * => if r.f().requires() then !r.f()() else false;
    r.f := bounded.certify(0, () => knot); // error: () ~> bool in the result
    assert r.f.erase() == knot;
    assert knot.requires();
    assert knot() == !knot();
  }
}
// The same through a generic setter, which only the check of type parameters that may be stored sees (https://github.com/erniecohen/dafny/issues/481); without that check, this proves false.
module GenericSetter {
  class Holder<T> {
    ghost var f: T
    constructor (ghost f: T) ensures this.f == f { this.f := f; }
    method Set(ghost x: T) modifies this ensures f == x { f := x; }
  }
  method Main() ensures false {
    var r := new Holder<bounded<nat, () -> (() ~> bool)>>(bounded.certify(0, () => (() => true))); // error: () ~> bool in the result, through the type parameter
    ghost var knot := () reads * => if r.f().requires() then !r.f()() else false;
    r.Set(bounded.certify(0, () => knot));
    assert r.f.erase() == knot;
    assert knot() == !knot();
  }
}
