// Replacement in a recursive group remains unsupported; refinement and abstract groups have their own controls.
// RUN: %exits-with 2 %resolve --member-invariants --recursive-modules "%s" > "%t"
// RUN: %resolve --member-invariants --recursive-modules --allow-warnings "%S/Inputs/git-issue-466-inherited-invariant.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// a member that replaces a module
replaceable module Service {
  function F(): int
}
module Replacing1 replaces Service {
  import Replacing2
  function F(): int { Replacing2.G() }
}
module Replacing2 {
  import Replacing1
  function G(): int { 0 }
}
