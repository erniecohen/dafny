// RUN: %exits-with 2 %baredafny resolve --type-system-refresh:false --use-basename-for-filename --show-snippets:false "%s" > "%t"
// RUN: %exits-with 2 %baredafny resolve --type-system-refresh:true --use-basename-for-filename --show-snippets:false "%s" >> "%t"
// RUN: %baredafny resolve --type-system-refresh:false --use-basename-for-filename --show-snippets:false "%S/Inputs/github-issue-44-acyclic.dfy" >> "%t"
// RUN: %baredafny resolve --type-system-refresh:true --use-basename-for-filename --show-snippets:false "%S/Inputs/github-issue-44-acyclic.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// A cyclic type used by a later declaration made the resolver loop forever,
// instead of reporting the cycle.

// The issue's program
module Alias {
  type Loop = Loop  // error: cycle
  datatype FT = FT(l: Loop)
}

// A cycle of two synonyms, closed before the datatype that uses it
module TwoSynonyms {
  type A = B  // error: cycle
  type B = A
  datatype D = D(a: A)
}

module Codatatype {
  type Loop = Loop  // error: cycle
  codatatype C = C(l: Loop)
}

// A cycle through the argument of a generic synonym
module ThroughGeneric {
  type Id<X> = X
  type L = Id<L>  // error: cycle
  datatype D = D(l: L)
}

module SubsetTypes {
  type S = x: T | true  // error: cycle
  type T = y: S | true
  datatype D = D(s: S)
}

// The datatype between the members of the cycle
module DatatypeBetween {
  type A = B  // error: cycle
  datatype D = D(a: A)
  type B = A
}

// The datatype before the cycle
module DatatypeFirst {
  datatype D = D(a: A)
  type A = B  // error: cycle
  type B = A
}
