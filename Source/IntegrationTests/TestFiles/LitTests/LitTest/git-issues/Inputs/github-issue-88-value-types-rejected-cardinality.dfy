// Programs in which a datatype gains a parent in a refinement, rejected by the cardinality
// analysis of general traits.

// ----- An added parent that closes an expansive cycle

module Cardinality0 {
  trait V {}
  datatype D = D(f: V -> bool)
}

module Cardinality1 refines Cardinality0 {
  datatype D extends V ... {} // error: V would include D, which stores V -> bool
}

// ----- A type parameter that the added parent does not keep

module Retained0 {
  trait S<Y> {}
  datatype G<X> = Mk(x: X)
}

module Retained1 refines Retained0 {
  datatype G<X> extends S<seq<X>> ... {} // error: X is not a direct type argument of S
}
