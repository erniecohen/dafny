// RUN: %exits-with 4 %verify "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// A qualified module id whose leading components name modules that enclose it
// depends on the module it reaches, not on the enclosing module, which would
// always be a cycle (https://github.com/erniecohen/dafny/issues/90).

// The reported program: a sibling refinement through a shared name prefix.
module A.B { function F(): int { 1 } }
module A.C refines A.B { lemma L() ensures F() == 1 {} }

// The same with the parent written out, and imports through the parent.
module P {
  module B { function G(): int { 2 } }
  module C refines P.B { }
  module D {
    import P.B
    import Q = P.C
    lemma M() ensures B.G() == 2 && Q.G() == 2 {}
    lemma Fails() ensures B.G() == 3 {} // error: B.G() is 2
  }
  import PB = P.B // equivalent to "import PB = B"
  lemma N() ensures PB.G() == 2 {}
}

// Several enclosing components are skipped.
module X.Y.Z { const k := 3 }
module X.Y.W { import X.Y.Z  lemma K() ensures Z.k == 3 {} }

// A path into a module nested in the importing one.
module E {
  module C {
    module D { const d := 6 }
    import DD = E.C.D
    lemma Dd() ensures DD.d == 6 {}
  }
}

// After the enclosing components, the path goes through default exports, as before.
module R {
  module S { module T { const t := 4 } }
  module U { import R.S.T  lemma O() ensures T.t == 4 {} }
}

// An abstract import through the enclosing module.
abstract module Ab {
  module B { const b := 7 }
  abstract module C { import X : Ab.B  lemma Bb() ensures X.b == 7 {} }
}

// From inside V, V.B is the B declared in V, as the plain name B is: V's export
// set, which does not mention B, is not consulted for it.
module V {
  export provides h
  const h := 0
  module B { const b := 5 }
  module C { import V.B  lemma Vb() ensures B.b == 5 {} }
}
