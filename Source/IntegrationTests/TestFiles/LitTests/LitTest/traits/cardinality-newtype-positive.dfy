// RUN: %verify --general-traits=full --general-newtypes "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

trait V {}
datatype Payload = Payload(n: int)
newtype VP extends V = p: seq<Payload> | true witness *

abstract module RefinementTemplate {
  trait V {}
  type D extends V
}
module Refinement refines RefinementTemplate {
  datatype Payload = Payload(n: int)
  newtype D = p: seq<Payload> | true witness *
}
