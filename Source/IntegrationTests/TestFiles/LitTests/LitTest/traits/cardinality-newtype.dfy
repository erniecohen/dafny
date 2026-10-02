// RUN: %exits-with 2 %resolve --general-traits=full --general-newtypes "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// Existing newtype base-type restrictions may reject this before cardinality validation.
// Preserve those restrictions; this is not new-checker coverage in that configuration.
trait V {}
datatype Payload = Payload(p: V -> bool)
newtype VP extends V = p: Payload | true witness *
