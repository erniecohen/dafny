// Recursion across module boundaries (https://github.com/erniecohen/dafny/issues/466), second
// stage: programs with recursive module groups that the resolver rejects while it resolves names
// and types.  Each is rejected for the reason its declarations would be rejected in one module,
// or, for the export sets, in a module that imports them.
// RUN: %exits-with 2 %resolve --member-invariants --recursive-modules "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// a cycle of type synonyms
module Synonym1 {
  import Synonym2
  type T = Synonym2.U
}
module Synonym2 {
  import Synonym1
  type U = Synonym1.T
}

// a name that two opened imports declare
module Ambiguous1 {
  import opened Ambiguous2
  import opened Other
  function F(): int { X() }
}
module Ambiguous2 {
  import Ambiguous1
  function X(): int { 1 }
}
module Other {
  function X(): int { 2 }
}

// an export set whose signature mentions a type that it does not export
module Export1 {
  export provides F
  import Export2
  function F(): Export2.T { Export2.Mk() }
}
module Export2 {
  import Export1
  datatype T = T
  function Mk(): T { T }
}

// a function that an export set hides from the other members
module Hidden1 {
  export provides F, Hidden2
  import Hidden2
  function F(): int { Hidden2.G() }
  function Secret(): int { 0 }
}
module Hidden2 {
  import Hidden1
  function G(): int { Hidden1.Secret() }
}

// imports that name their targets through each other
module Path1 {
  import Path2
  import X = Path2.Y
}
module Path2 {
  import Path1
  import Y = Path1.X
}

// a name that two opened imports declare as the same type: outside a group the name is not
// ambiguous; here one of the two is a member, whose declaration is not resolved yet, so it is
module SameType1 {
  import opened SameType2
  import opened SameTypeBase
  function F(x: T): T { x }
}
module SameType2 {
  import SameType1
  import SameTypeBase
  type T = SameTypeBase.T
}
module SameTypeBase {
  datatype T = A | B
}

// an error in a module that a member imports from outside the group: the group is not resolved,
// and an error says why (so the type error in Failed2 is not reported)
module BelowWithError {
  function Bad(): int { true }
}
module Failed1 {
  import Failed2
  import BelowWithError
  function F(): int { BelowWithError.Bad() }
}
module Failed2 {
  import Failed1
  function G(): int { Failed1.F() + true }
}
