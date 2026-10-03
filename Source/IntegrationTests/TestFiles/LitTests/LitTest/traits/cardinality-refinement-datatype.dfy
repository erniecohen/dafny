// RUN: %exits-with 2 %resolve "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

abstract module A {
  trait V {}
  type D extends V
}
module B refines A {
  datatype D = Ground | D(p: V -> bool)
}
