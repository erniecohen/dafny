// False controls: the positive program of github-issue-266-m3.dfy, with a reachable "assert false" in each family as
// its only failures.
// The positive storage control of the design (section 3.8): a bound-zero function copied through generic identity,
// a mutable field, a sequence and a datatype, applied twice under bound 1.  Replacing the field's value does not
// change the copies.
module Storage {
  type Zero = h: bounded<nat, int -> int> | h.bound == 0
    witness bounded.certify(0, (x: int) => x)
  function Id<T>(x: T): T { x }
  datatype Option<T> = None | Some(value: T)
  class Holder<T> {
    var f: T
    constructor (f: T) ensures this.f == f { this.f := f; }
  }
  function Twice(z: Zero, x: int): int decreases {:global} 1 { z(z(x)) }
  method Flow() decreases {:global} 1 {
    var inc: Zero := bounded.certify(0, (x: int) => x + 1);
    var h := new Holder(Id(inc));
    var s := [h.f, inc];
    var o := Some(s[1]);
    var a := Twice(h.f, 20);
    h.f := bounded.certify(0, (x: int) => x + 2);
    assert a == 22 && Twice(h.f, 20) == 24 && Twice(s[0], 20) == 22 && Twice(o.value, 20) == 22;
    assert false; // error: the only failure
  }
}

// An observer: a subject keeps, in a mutable field, bounded values whose functions read the heap, and spends their
// budget when it notifies them.
module Observer {
  class Counter {
    var n: int
    constructor () { n := 0; }
  }
  type Obs = h: bounded<nat, () ~> int> | h.bound == 0
    witness bounded.certify(0, () => 0)
  class Subject {
    var obs: seq<Obs>
    constructor () ensures obs == [] { obs := []; }
    method Attach(o: Obs) modifies this ensures obs == old(obs) + [o] { obs := obs + [o]; }
    method Notify() returns (total: int)
      requires forall i | 0 <= i < |obs| :: obs[i].requires()
      decreases {:global} 1
    {
      total := 0;
      for i := 0 to |obs| {
        total := total + obs[i]();
      }
    }
  }
  method Watch() decreases {:global} 1 {
    var c := new Counter();
    var s := new Subject();
    s.Attach(bounded.certify(0, () reads c => c.n));
    c.n := 7;
    var t := s.Notify();
    assert false; // error: the only failure
  }
}

// A callback kept for later: a parameter with a callback bound is certified under a bound at least its own, on
// entry, and stored; a lambda can apply it, or pass it on, under a larger bound.
module Keep {
  class Cell {
    var k: nat
    constructor () { k := 0; }
  }
  class Kept {
    var cb: bounded<nat, int -> int>
    constructor (cb: bounded<nat, int -> int>) ensures this.cb == cb { this.cb := cb; }
  }
  method Store(f: int -> int) returns (k: Kept)
    decreases {:global} 3
    decreases {:callback f} 1
    ensures k.cb.bound <= 1
  {
    k := new Kept(bounded.certify(1, f));
  }
  method Spend(k: Kept, x: int) returns (r: int) requires k.cb.bound <= 1 decreases {:global} 2 {
    r := k.cb(x);
  }
  method OnEntry(cell: Cell, f: int -> int) returns (c: bounded<nat, int -> int>)
    modifies cell
    decreases {:global} 9
    decreases {:callback f} cell.k
  {
    var k := cell.k;
    cell.k := 0;
    c := bounded.certify(k, f);
  }
  method Capture(f: int -> int) returns (c: bounded<nat, () -> int>)
    decreases {:global} 9
    decreases {:callback f} 2
    ensures c.bound == 3
  {
    c := bounded.certify(3, () => f(1));
    assert false; // error: the only failure
  }
  function Pass(g: int -> int, x: int): int
    decreases {:global} 4
    decreases {:callback g} 3
  {
    g(x)
  }
  method Forward(f: int -> int) returns (c: bounded<nat, int -> int>)
    decreases {:global} 9
    decreases {:callback f} 3
  {
    c := bounded.certify(5, (x: int) => Pass(f, x));
    assert false; // error: the only failure
  }
  method Nested(f: int -> int) returns (c: bounded<nat, () -> (() -> int)>)
    decreases {:global} 9
    decreases {:callback f} 1
  {
    c := bounded.certify(2, () => () => f(0));
  }
  function InFunction(f: int -> int): bounded<nat, () -> int>
    decreases {:global} 9
    decreases {:callback f} 2
  {
    bounded.certify(3, () => f(1))
  }
}

// Higher-order bounded values: a bounded value whose function takes bounded values.
module HigherOrder {
  type Zero = h: bounded<nat, int -> int> | h.bound == 0
    witness bounded.certify(0, (x: int) => x)
  type One = h: bounded<nat, Zero -> int> | h.bound == 1
    witness bounded.certify(1, (z: Zero) => 0)
  method Apply() decreases {:global} 2 {
    var app: One := bounded.certify(1, (z: Zero) => z(3) + z(4));
    var r := app(bounded.certify(0, (x: int) => 10 * x));
    assert r == 70;
    assert false; // error: the only failure
  }
}
