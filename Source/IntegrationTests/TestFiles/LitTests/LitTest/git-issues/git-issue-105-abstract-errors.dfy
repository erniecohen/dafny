// RUN: %exits-with 2 %resolve --member-invariants "%s" > "%t"
// RUN: %exits-with 2 %resolve --member-invariants "%S/Inputs/git-issue-105-abstract-witness.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) of abstract types: the errors
// of section 4.3 of docs/dev/issue105-member-invariants-plan.md.  An abstract type declares no
// witness: its characteristics, '(0)' or '(00)', promise one, and each refinement that defines the
// type supplies it.  A type synonym cannot refine an abstract type with members.  The extension
// condition holds of the specifications in the abstract module and of the bodies in a refinement.
// 'raw' does not yet apply to an abstract type that is only provided.

abstract module Spec {
  type T(0) {
    function Size(): int
    invariant Size() > 0
  }
  type U(00) {
    function Size(): int
    invariant Size() > 0
  }
}

module NoWitness refines Spec {
  datatype T = MkT(n: int) {                           // error: no witness, so T may be empty
    function Size(): int { n }
  }
  datatype U = MkU(n: int) {                           // error: likewise
    function Size(): int { n }
  }
}

module GhostWitness refines Spec {
  datatype T = MkT(n: int) {                           // error: (0) needs a compiled witness
    function Size(): int { n }
    ghost witness MkT(1)
  }
  datatype U = MkU(n: int) {
    function Size(): int { n }
    ghost witness MkU(1)
  }
}

module Synonym refines Spec {
  type T = int                                         // error: a synonym declares no members
}

abstract module Extension0 {
  type T(!new) {
    function Size(): int
      requires forall t: T :: true                     // error: the invariant uses Size
    invariant Size() > 0
  }
}
abstract module ExtensionV0 {
  type V {
    ghost function Size(): int
    invariant Size() > 0
  }
}
module ExtensionV1 refines ExtensionV0 {
  datatype V = MkV(n: int) {
    ghost function Size(): int {
      if forall v: V :: v.n != n then 1 else 2         // error: likewise
    }
  }
}

abstract module Exported {
  export Provides provides T, T.Size
  type T(0) {
    function Size(): int
    invariant Size() > 0
  }
}
abstract module ProvidedClient {
  import S = Exported`Provides
  method M(r: raw S.T) { }                             // error: not yet supported
}

// The promise '(0)' or '(00)' of an abstract type with invariants is kept by the witness of each
// refinement that defines the type.  Only an abstract module is refined before any concrete use, so
// only it may make the promise: a client of any other module would take the promise as written, and
// each lemma below would prove false.  The positive control is 'WitnessedClient' in
// git-issue-105-abstract-types.dfy.  (The module 'Synonym' above inherits 'U(00)' unrefined.)
module M { type T(0) { invariant false } }             // error: M is not abstract
module Client { import M  lemma X() ensures false { var t: M.T :| true; } }
module SameModule {
  type T(00) { invariant false }                       // error: likewise
  lemma X() ensures false { var t: T :| true; }
}
abstract module Promise {
  type T(0) { invariant false }                        // error: for Unrefined, which inherits it
}
module Unrefined refines Promise { }
module UnrefinedClient { import Unrefined  lemma X() ensures false { var t: Unrefined.T :| true; } }
replaceable module Replaceable {
  type T(0) { invariant false }                        // error: nothing need replace the module
}
module ReplaceableClient { import Replaceable  lemma X() ensures false { var t: Replaceable.T :| true; } }
module Inherited {
  trait Tr { invariant false }
  type T(0) extends Tr { }                             // error: an inherited invariant counts too
  lemma X() ensures false { var t: T :| true; }
}
abstract module Outer {
  module Inner { type T(0) { invariant false } }       // error: Inner is not abstract
}
abstract module Kept {
  type T(0) { invariant false }                        // abstract, and refined by nothing here
  lemma X() ensures false { var t: T :| true; }        // so this proof holds only of refinements
}
