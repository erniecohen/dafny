// Member invariants (https://github.com/erniecohen/dafny/issues/105): an abstract type declares no
// witness, a check made before the program is resolved further.

abstract module Spec {
  type W(0) {
    invariant true
    witness 0                                          // error: an abstract type declares no witness
  }
}
