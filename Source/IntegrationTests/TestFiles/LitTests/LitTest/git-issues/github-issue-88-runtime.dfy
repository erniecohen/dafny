// RUN: %testDafnyForEachCompiler --compilers cs,js --refresh-exit-code 0 "%s" -- --refinement-extends --solver-path %review-z3
// RUN: %exits-with 4 %verify --refinement-extends "%S/Inputs/github-issue-88-runtime-false.dfy" > "%t"
// RUN: %exits-with 4 %verify --refinement-extends --type-system-refresh:false --general-newtypes:false "%S/Inputs/github-issue-88-runtime-false.dfy" >> "%t"
// RUN: %diff "%S/Inputs/github-issue-88-runtime-false.expect" "%t"

// Added parents must reach the compiled representation, including generic diamonds,
// inherited constructors, fields and default methods, and parents added directly to a class.
module Base {
  trait P<A> extends object {
    function Value(a: A): A decreases {:global} 0
  }
  trait T<A> extends P<A> {}
  class C extends T<int> {
    constructor () {}
    function Value(a: int): int { a + 1 }
  }
  class D {
    constructor () {}
    function Value(a: int): int { a + 2 }
  }
}

module Refined refines Base {
  trait S<A> extends P<A> {
    var count: int
    method Twice(a: A) returns (r: A)
      ensures r == Value(Value(a))
    {
      r := Value(Value(a));
    }
  }
  trait T<A> ... extends S<A> {}
  class D ... extends S<int> {}

  method Main() {
    var c := new C();
    var p: P<int> := c;
    var s: S<int> := c;
    c.count := 9;
    var r := s.Twice(3);
    assert r == 5;
    print p.Value(2), " ", r, " ", c.count, "\n";

    var d := new D();
    s := d;
    s.count := 11;
    r := s.Twice(3);
    assert r == 7 && d.count == 11;
    print d.Value(2), " ", r, " ", d.count, "\n";
  }
}
