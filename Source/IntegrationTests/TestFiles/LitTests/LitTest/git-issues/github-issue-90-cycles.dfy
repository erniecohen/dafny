// RUN: %exits-with 2 %resolve "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// Genuine cycles through qualified module ids are still rejected
// (https://github.com/erniecohen/dafny/issues/90).

// A module refining its enclosing module.
module N1 { module C refines N1 { } }

// A module importing a module that encloses it.
module N2 { module C { import N2.C } }

// A module refining itself through its enclosing module.
module N3 { module C refines N3.C { } }

// Siblings importing each other.
module N4 { module B { import N4.C } module C { import N4.B } }

// Siblings refining each other.
module N5 { module B refines N5.C { } module C refines N5.B { } }

// A path whose next component is an import of the enclosing module, not a
// literal module, keeps the enclosing module as its root, as before.
module N6 { module B { }  import I = B  module C { import N6.I } }
