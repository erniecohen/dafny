// Controls for https://github.com/erniecohen/dafny/issues/410: refinements that keep the meaning of
// every inherited name are accepted.  The one error is the vacuity control at the end.

// Ordinary refinement: an opened import, abstract members, a class with a type parameter,
// patterns, a nested module, generic functions, let patterns and a statement skeleton.
module L1 { function F(): int { 1 } }
abstract module A1 {
  import opened L1
  type T
  const c: int
  function G(t: T): int
  class C<U> { function H(u: U): U { u } }
  datatype D = Foo | Bar(n: int)
  lemma P() ensures F() == 1 { }
  function M(d: D): int { match d case Foo => 0 case Bar(k) => k case _ => 7 }
  function N(d: D): int { match d case Foo => 0 case other => 1 }
  method Q(t: T) returns (r: int) ensures r == G(t) { r := G(t); var k := c; assert k == c; }
  module N1 { function F(): int { 3 } }
  lemma R() ensures N1.F() == 3 { }
  function S<V>(v: V): V { v }
  lemma Lets() { var (a, b) := (1, 2); assert a + b == 3; var Bar(z) := Bar(4); assert z == 4; }
}
module B1 refines A1 {
  type T = int
  const c: int := 5
  function G(t: T): int { t }
  class C<U> ... { function Extra(): int { 3 } }
  method Q(t: T) returns (r: int) ensures r == G(t) { ...; }
  lemma Ok() ensures F() == 1 { P(); }
}
module B1a refines B1 { }

// A name that the refinement adds, used only by the refinement's own code.
module L2 { function F(): int { 1 } }
module A2 {
  import opened L2
  lemma P() ensures true { }
}
module B2 refines A2 {
  function F(): int { 2 }
  lemma Mine() ensures F() == 2 { }
}

// Abstract imports bound to refinements of their targets, including a nested module of the target.
abstract module I3 {
  type T
  function f(): int
  module Inner { function g(): int { 7 } }
}
abstract module A3 {
  import X : I3
  function F(t: X.T): int { X.f() + X.Inner.g() }
  lemma P() ensures X.Inner.g() == 7 { }
  function Id(t: X.T): X.T { match t case x => x }
}
module I3a refines I3 { type T = int  function f(): int { 1 } }
module I3b refines I3a { }
module B3 refines A3 { import X = I3a }
abstract module B3a refines A3 { import X : I3b }
module B3b refines B3a { import X = I3b }

// A trait member that a class overrides, and a statement skeleton that keeps its names.
module A4 {
  trait Tr { function g(): int }
  class K extends Tr { function g(): int { 1 } }
  method M(k: K) returns (r: int) { var y := 1; r := y + k.g(); }
  method W() { var i := 0; while i < 3 invariant i <= 3 { i := i + 1; } }
}
module B4 refines A4 {
  method M ... { ...; }
}

// The default decreases clause of an inherited member is the base's: defining the parameter's
// abstract type as a datatype does not add the parameter to it.
abstract module A5 {
  type T
  function G(t: T, n: nat): int { if n == 0 then 0 else G(t, n - 1) }
  method M(t: T, n: nat) { if n > 0 { M(t, n - 1); } }
}
module B5 refines A5 {
  datatype T = Leaf | Node(T)
  function K(t: T, n: nat): int { G(t, n) }
}

// Vacuity control: the accepted refinements above do not prove false.
module B6 refines A2 {
  lemma Vacuous() ensures false { P(); }
}
