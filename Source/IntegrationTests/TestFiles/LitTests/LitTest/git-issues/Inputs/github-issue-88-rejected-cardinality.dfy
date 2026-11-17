// A program that --refinement-extends rejects in the cardinality analysis of general traits.

// ----- An added parent that closes an expansive cycle of general traits

module Cardinality0 {
  trait V {}
  trait W {}
  datatype D extends W = D(f: V -> bool)
}
module Cardinality1 refines Cardinality0 {
  trait W ... extends V {} // error: V now includes D, which stores V -> bool
}
