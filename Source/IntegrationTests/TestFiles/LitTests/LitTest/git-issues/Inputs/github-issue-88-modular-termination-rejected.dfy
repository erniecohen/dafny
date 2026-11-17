// A program that --refinement-extends rejects under --modular-termination: a parent added in a
// refinement must be open if it comes from another module.

module Other {
  trait Sealed {
    function G(): int
  }
}

module Sealed0 {
  import Other
  trait T {
    function G(): int {
      1
    }
  }
}

module Sealed1 refines Sealed0 {
  trait T ... extends Other.Sealed {} // error: Other.Sealed is sealed
}

module SealedControl {
  import Other
  trait T extends Other.Sealed { // error: Other.Sealed is sealed
    function G(): int {
      1
    }
  }
}
