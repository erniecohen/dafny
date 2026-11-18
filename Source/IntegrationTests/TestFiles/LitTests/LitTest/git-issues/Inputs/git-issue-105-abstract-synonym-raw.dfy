// Member invariants (https://github.com/erniecohen/dafny/issues/105): under
// --synonym-refines-abstract-type, 'raw T' in inherited code where 'T' is a synonym.

abstract module Spec {
  type T {
    function Size(): int
    invariant Size() > 0
  }
  function Raw(r: raw T): int { r.Size() }
}
module Impl refines Spec {
  type T = D
  datatype D = MkD(n: int) {
    function Size(): int { n }
  }
}
