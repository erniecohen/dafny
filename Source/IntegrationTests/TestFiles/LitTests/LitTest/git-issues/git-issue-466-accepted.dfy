// Recursion across module boundaries (https://github.com/erniecohen/dafny/issues/466), first stage:
// control.  A program whose modules do not import each other is accepted with --recursive-modules
// as it is without it, under both resolvers, whatever kinds of module dependency it has: a diamond
// of imports, refinement, an abstract import and its binding, a replaceable module and its
// replacement, nested modules, and a codatatype.  The verifier still verifies it: the only error is
// the one for 'assert false'.
// RUN: %exits-with 4 %baredafny verify --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh:false "%s" > "%t"
// RUN: %exits-with 4 %verify "%s" >> "%t"
// RUN: %exits-with 4 %verify --member-invariants --recursive-modules --show-hints:false "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

module Bottom {
  function D(n: nat): nat { n }
}
module Left {
  import Bottom
  function L(n: nat): nat { Bottom.D(n) }
}
module Right {
  import opened Bottom
  function R(n: nat): nat { D(n) }
}
module Top {
  import Left
  import X = Right
  lemma Same(n: nat)
    ensures Left.L(n) == X.R(n)
  {
  }
}

abstract module Spec {
  import Bottom
  type T
  function Value(t: T): nat
  function Twice(t: T): nat { Bottom.D(Value(t)) + Value(t) }
}
module Impl refines Spec {
  type T = nat
  function Value(t: T): nat { t }
}

module I {
  function H(n: nat): nat
}
module J refines I {
  function H(n: nat): nat { n }
}
abstract module User {
  import X : I
  function F(n: nat): nat { X.H(n + 1) }
}
module UserImpl refines User {
  import X = J
}

replaceable module Service {
  function Answer(): nat
}
module Client {
  import Service
  function Ask(): nat { Service.Answer() }
}
module ServiceImpl replaces Service {
  import Bottom
  function Answer(): nat { Bottom.D(42) }
}

module Nest {
  module Inner1 {
    const c := 1
  }
  module Inner2 {
    import Inner1
    const d := Inner1.c
  }
  const e := Inner2.d
}

module Co {
  codatatype Stream = Cons(head: int, tail: Stream)
  function Head(s: Stream): int { s.head }
}

module Control {
  import Top
  import Nest
  lemma False()
    ensures Nest.e == 1
  {
    Top.Same(0);
    assert false; // error
  }
}
