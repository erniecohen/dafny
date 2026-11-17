// Recursion across module boundaries (https://github.com/erniecohen/dafny/issues/466), first stage:
// only plain imports may form a recursive module group.  With --recursive-modules, a cycle that goes
// through a refinement, a replacement, an abstract import, the binding of an inherited abstract
// import, or the nesting of a module in another is an error that names that edge, and so is a cycle
// that goes through a replaceable module.  A module that imports itself is not a group.
// Without the option, each is the cycle error that it was before, under both resolvers.
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh:false "%s" > "%t"
// RUN: %exits-with 2 %resolve "%s" >> "%t"
// RUN: %exits-with 2 %resolve --member-invariants --recursive-modules --show-hints "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// refinement: the refined module imports the module that refines it
module RefSpec {
  import RefImpl
  type T
  function F(t: T): int
}
module RefImpl refines RefSpec {
  type T = int
  function F(t: T): int { 0 }
}

// refinement as one edge of a longer cycle; the other edges are plain imports
module Ring1 {
  import Ring2
  type T
}
module Ring2 {
  import Ring3
}
module Ring3 refines Ring1 {
  type T = int
}

// abstract import: its target imports the module that declares it
abstract module AbsI {
  import AbsU
  function H(n: nat): nat
}
abstract module AbsU {
  import X : AbsI
  function F(n: nat): nat { X.H(n) }
}

// binding of an inherited abstract import: BindUser.F calls X.H, and the binding makes X the module
// BindImpl, whose H calls F
module BindI {
  function H(n: nat): nat
}
abstract module BindUser {
  import X : BindI
  function F(n: nat): nat { X.H(n + 1) }
}
module BindImpl refines BindI {
  import BindUserImpl
  function H(n: nat): nat { if n == 0 then 0 else BindUserImpl.F(n - 1) + 1 }
}
module BindUserImpl refines BindUser {
  import X = BindImpl
}

// the same, where the abstract import is inherited through two refinements
module Bind2I {
  function H(n: nat): nat
}
abstract module Bind2User0 {
  import X : Bind2I
  function F(n: nat): nat { X.H(n + 1) }
}
abstract module Bind2User1 refines Bind2User0 {
}
module Bind2Impl refines Bind2I {
  import Bind2UserImpl
  function H(n: nat): nat { if n == 0 then 0 else Bind2UserImpl.F(n - 1) + 1 }
}
module Bind2UserImpl refines Bind2User1 {
  import X = Bind2Impl
}

// nesting: a module imported by a module that its submodule imports
module NestP {
  module Q {
    import NestR
    const c := NestR.d
  }
  const e := 0
}
module NestR {
  import NestP
  const d := NestP.e
}

// nesting: an import of P.Q depends on P, the first component of the path
module PathP {
  module Q {
    import PathR
    const c := 0
  }
}
module PathR {
  import PathP.Q
  const d := Q.c
}

// a replaceable module in a cycle of plain imports
replaceable module ReplF {
  import ReplG
  function H(): int
}
module ReplG {
  import ReplF
  function K(): int { ReplF.H() }
}

// replacement as an edge of a cycle
replaceable module Repl2F {
  import Repl2G
  function H(): int
}
module Repl2G {
  import Repl2R
}
module Repl2R replaces Repl2F {
  function H(): int { 0 }
}

// a module that imports itself, by its name and through a submodule
module Self1 {
  import Self1
}
module Self2 {
  import X = Self2.Inner
  module Inner { }
}

// two imports that name each other
module Aliases {
  import X = Y
  import Y = X
}
