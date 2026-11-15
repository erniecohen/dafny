// False controls: each family of the positive program, with a reachable "assert false" as its only failure.
module Flows {
  datatype Box<T> = Box(t: T)
  class Holder {
    var f: bounded<nat, int -> int>
    constructor (f: bounded<nat, int -> int>) { this.f := f; }
  }
  function Id<T>(x: T): T { x }
  method Pass(c: bounded<nat, int -> nat>, d: bounded<nat, int -> int>, o: bounded<ORDINAL, int ~> int>)
    returns (r: bounded<nat, int --> int>, s: seq<bounded<nat, int -> int>>, b: Box<bounded<nat, int -> int>>)
  {
    r := c;
    s := [d, c];
    b := Box(Id(d));
    var h := new Holder(d);
    h.f := c;
    var m: map<int, bounded<nat, int -> int>> := map[0 := c];
    assert false; // error: the only failure
  }
  lemma Members(c: bounded<nat, int -> int>, d: bounded<nat, int -> int>) {
    ghost var p := {c, d};
    assert c in p;
    assert false; // error: the only failure
  }
}
module Shadow {
  type bounded<A, B> = int
  method M(x: bounded<int, int>) returns (y: int) { y := x + 1; assert false; } // error
}
module Lib { type bounded = int }
module Opened {
  import opened Lib
  method M(x: bounded) returns (y: int) { y := x; assert false; } // error
}
