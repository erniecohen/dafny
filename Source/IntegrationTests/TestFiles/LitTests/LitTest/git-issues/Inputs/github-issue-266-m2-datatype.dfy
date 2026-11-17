// Knots through data.  A datatype cannot hold a bounded value of a function on itself: positivity applies to the
// payload as to any arrow.  K7: a bounded value has no writable alias at its erased type, since a bounded type is
// not a subtype of its payload and arrays are non-variant.
module R {
  datatype D = D(h: bounded<nat, D -> bool>) // error: D occurs in its own payload's domain
}
module K7 {
  trait T extends object { }
  type Zero = h: bounded<nat, T -> int> | h.bound == 0
    ghost witness bounded.certify(0, (t: T) => 0)
  method Alias(good: Zero) {
    var xs := new Zero[1](i => good);
    var rawAlias: array<T -> int> := xs; // error: no writable erased alias
  }
}
// K3: installing into a mutable field a value whose function reads the heap, which a later state could close into
// a loop, is still rejected by the rule for any function value that reads the heap.
module K3 {
  class Cell {
    var cb: bounded<nat, () ~> int>
    constructor() { cb := bounded.certify(0, () => 0); }
  }
  method Main() {
    var c := new Cell();
    var g := bounded.certify(1, () reads * => c.cb());
    c.cb := g; // error: a function value that reads the heap
    var result := g();
  }
}
