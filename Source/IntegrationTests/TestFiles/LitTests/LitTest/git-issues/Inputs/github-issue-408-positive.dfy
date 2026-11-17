// Controls for https://github.com/erniecohen/dafny/issues/408 (RUN lines in ../github-issue-408.dfy):
// every binding of an inherited abstract import to a refinement of its target is still accepted.
// Only Vacuity's false claim fails, which shows that P's contract is still assumed.

abstract module A {
  function F(): int
  lemma L() ensures F() > 0
}

abstract module A2 refines A {
  lemma L2() ensures F() > 0 { L(); }
}

module Impl refines A {
  function F(): int { 1 }
  lemma L() { }
}

module Impl2 refines A2 {
  function F(): int { 2 }
  lemma L() { }
}

module ImplE refines A {
  export E provides F, L
  function F(): int { 5 }
  lemma L() { }
}

abstract module B {
  import X : A
  lemma P() ensures X.F() > 0 { X.L(); }
}

// Alias imports of a refinement, a refinement of a refinement, and an export set of a refinement
module C1 refines B {
  import X = Impl
  lemma Q() ensures X.F() > 0 { P(); }
}
module C2 refines B { import X = Impl2 }
module C9 refines B { import X = ImplE`E }

// Nested modules that refine A directly or through A2
module C3 refines B {
  module X refines A {
    function F(): int { 3 }
    lemma L() { }
  }
}
module C4 refines B {
  module X refines A2 {
    function F(): int { 4 }
    lemma L() { }
  }
}

// Abstract imports of A or of a refinement of A, then bound again further down
abstract module C5 refines B { import X : A2 }
abstract module C6 refines B { import X : A }
module C7 refines C5 { import X = Impl2 }

// An alias of another module's abstract import of A
abstract module D { import Y : A }
abstract module C8 refines B { import X = D.Y }

module Vacuity refines B {
  import X = Impl
  lemma Bad() ensures false { P(); }
}
