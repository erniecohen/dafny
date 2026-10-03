// RUN: %exits-with 2 %resolve --general-traits=full --general-newtypes "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

trait V {}
datatype Payload = Payload(p: V -> bool)
newtype VP extends V = p: seq<Payload> | true witness *
