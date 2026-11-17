// Knots through data.  A datatype cannot hold a bounded value of a function on itself: positivity applies to the
// payload as to any arrow.  K7: a bounded value has no writable alias at its erased type, since a bounded type is
// not a subtype of its payload and arrays are non-variant.  (K3, a heap-reading value installed in a mutable field,
// is a verification failure since milestone 3: github-issue-266-m3.dfy.)
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
