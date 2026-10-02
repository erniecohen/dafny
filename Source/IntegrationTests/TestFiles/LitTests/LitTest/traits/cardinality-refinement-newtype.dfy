// RUN: %exits-with 2 %resolve --type-system-refresh --general-traits=full --general-newtypes "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

abstract module A {
  trait V {}
  type D extends V
}
module B refines A {
  datatype Payload = Payload(p: V -> bool)
  newtype D = p: seq<Payload> | true witness *
}
