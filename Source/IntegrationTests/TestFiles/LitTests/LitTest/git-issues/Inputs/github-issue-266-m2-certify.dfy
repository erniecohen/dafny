// Values that "bounded.certify" cannot certify, and applications of values with no known bound.
module A {
  trait {:termination false} T {
    function F(): int
      decreases {:global} 1
  }
  function High(): int
    decreases {:global} 2
  {
    0
  }
  function Dep(n: nat, t: T): int
    decreases {:global} n + 2
  {
    t.F()
  }
  class Cell {
    var v: nat
    const t: T
    constructor (t: T) { this.t := t; }
    function Heap(): int
      reads this
      decreases {:global} v + 2
    {
      t.F()
    }
  }
  function Free(t: T): int {
    t.F()
  }
  function Callback(f: int -> int): int
    decreases {:callback f} 1
    decreases {:global} 2
  {
    0
  }
  method Raw() {
    var raw := High;
    var c := bounded.certify(3, raw); // error: not a lambda or named function
  }
  method Abstract(t: T) {
    var c := bounded.certify(3, t.F); // error: dispatch chooses the implementation
  }
  method Parameter() {
    var c := bounded.certify(3, Dep); // error: the bound depends on a parameter
  }
  method HeapBound(cell: Cell) {
    var c := bounded.certify(3, cell.Heap); // error: the bound depends on the heap
  }
  method NoBound() {
    var c := bounded.certify(3, Free); // error: no global bound
  }
  method WithCallback() {
    var c := bounded.certify(3, Callback); // error: a callback-bound parameter
  }
  method RawEscape(c: bounded<nat, int -> int>)
    decreases {:global} 9
  {
    var r := c.erase;
    var x := r(0); // error: unknown code under a bound
  }
}
