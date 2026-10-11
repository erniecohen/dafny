// RUN: %baredafny resolve --type-system-refresh:true --general-traits:datatype --general-newtypes:true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// Incremental edits are covered by IncrementalBitvectorMembersTest.
newtype Foo = bv64
