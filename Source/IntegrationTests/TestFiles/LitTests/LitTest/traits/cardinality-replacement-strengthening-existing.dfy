// RUN: %exits-with 2 %resolve "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// The ordinary variance rule already rejects a strict selected formal inherited
// through a permissive parent position. This is not new checker coverage.
abstract module A { trait V<!X> {} type D<!X> extends V<X> }
replaceable module B refines A {}
module C replaces B { datatype D<X> = D(x: X) }
