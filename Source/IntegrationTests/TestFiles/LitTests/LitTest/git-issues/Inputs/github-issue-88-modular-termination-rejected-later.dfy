// A program that --refinement-extends rejects under --modular-termination, in a later phase than
// the first one: a refinement may not give an open trait a sealed ancestor.

module Ancestor0 {
  trait Local {
    function G(): int
  }
  trait {:termination false} {:axiom} U {
    function G(): int {
      1
    }
  }
}

module Ancestor1 refines Ancestor0 {
  trait {:termination false} {:axiom} U ... extends Local {} // error: Local is sealed
}

module AncestorControl {
  trait Local {
    function G(): int
  }
  trait {:termination false} {:axiom} U extends Local { // error: Local is sealed
    function G(): int {
      1
    }
  }
}
