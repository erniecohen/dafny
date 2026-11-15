// RUN: %baredafny resolve --type-system-refresh:true --general-traits:datatype --general-newtypes:true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// Incremental edits are covered by IncrementalBitvectorMembersTest.
newtype V = bv64
trait Op<T> {
  function op2(t0: T, t1: T): T
}
