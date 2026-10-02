// RUN: %exits-with 2 %resolve "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

abstract module A { trait V<X> {} type D<!X> }
replaceable module B refines A {}
module C replaces B { datatype D<X> extends V<X> = D(x: X) }
