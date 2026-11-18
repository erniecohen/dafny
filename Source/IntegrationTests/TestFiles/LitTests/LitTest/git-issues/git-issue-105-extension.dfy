// Member invariants (https://github.com/erniecohen/dafny/issues/105): the extension condition.  The
// values of a type with invariants are the raw values that satisfy them.  If an invariant refers to the
// set of values of its own type, that is a recursive definition that may have no solution, and then
// the type's axioms are inconsistent (git-issue-105-vacuity.dfy shows it for Liar and Liar2).  So within
// the cone of an invariant (the functions, constants and types it reaches), the resolver rejects, for
// a type that depends on the type with the invariant: a quantifier over it that does not range over a
// collection, a generic function instantiated with it, and a test or conversion of a raw value to it.
// It also rejects an applied function value other than a lambda expression or a named function, and a
// dynamically bound function, whose bodies are not known.
// RUN: %exits-with 2 %resolve --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// The examples of section 6.2 of docs/dev/issue105-member-invariants-plan.md.
module Liars {
  datatype Liar = Liar(f: int) {
    invariant f == 0 ==> !(exists e: Liar :: e.f == 0)  // error: refers to the set of values of 'Liar'
  }

  datatype Liar2 = Liar2(f: int) {
    ghost predicate No() { !(this is Liar2) }  // error: tests a raw value against 'Liar2'
    invariant No()
  }
}

// Clause 1: a quantifier over a type that depends on T, unless it ranges over a collection.
module Quantifiers {
  datatype ThroughFunction = ThroughFunction(f: int) {
    ghost predicate P() { exists e: ThroughFunction :: e.f == f + 1 }  // error
    invariant P()
  }

  datatype ThroughSeq = ThroughSeq(f: int) {
    invariant forall s: seq<ThroughSeq> :: |s| == 0 || s[0].f != f  // error
  }

  datatype ThroughSubset = ThroughSubset(f: int) {
    invariant forall e: Small :: e.f != f  // error
  }
  type Small = t: ThroughSubset | t.f < 10 witness *

  datatype U = U(t: ThroughDatatype)
  datatype ThroughDatatype = ThroughDatatype(f: int) {
    invariant !(exists u: U :: u.t.f == f)  // error: U is built from ThroughDatatype
  }

  datatype V = V(x: int) {
    invariant x > 0 && !(exists t: ThroughOther :: t.f == x)  // error: V's invariant, about ThroughOther
  }
  datatype ThroughOther = ThroughOther(f: int, v: V) {
    invariant f > 0
  }

  datatype ThroughLet = ThroughLet(f: int) {
    invariant f == 0 ==> (var t: ThroughLet :| t.f == 1; t.f == 1)  // error
  }

  // The values of raw ThroughGeneric are built from those of Wrap<ThroughGeneric>.
  type Wrap<X(!new)> = s: seq<X> | (exists x: X :: true) ==> |s| < 10 witness *  // error
  datatype ThroughGeneric = ThroughGeneric(f: int, w: Wrap<ThroughGeneric>) {
    invariant f >= 0
  }
  // accepted: the quantifier ranges over a collection
  type Bounded<X> = s: seq<X> | forall x: X :: x in s ==> |s| > 0 witness *
  datatype ThroughBounded = ThroughBounded(f: int, w: Bounded<ThroughBounded>) {
    invariant f >= 0
  }

  // The constraint of the arrow type 'ThroughArrow -> bool' quantifies over ThroughArrow: a function value is
  // one of its values if its precondition holds at every value of ThroughArrow.
  datatype ThroughArrow = ThroughArrow(f: int) {
    invariant f == 0 ==> ((x: ThroughArrow) requires x.f != 0 => true) is ThroughArrow -> bool  // error
  }

  datatype RawElements = Node(f: int, kids: seq<raw RawElements>) {
    invariant forall k: RawElements | k in kids :: k.f <= f  // error: tests the raw elements
  }

  // accepted: the bound variables range over collections of finished values
  datatype Rose = Node(size: nat, kids: seq<Rose>) {
    invariant forall k <- kids :: k.size < size
  }
  datatype Bush = Node(f: int, kids: set<Bush>) {
    invariant (set k | k in kids && k.f > 0) <= kids && forall k: Bush :: k in kids ==> k.f < f
  }
}

// Clause 2: a generic function instantiated with a type that depends on T is analysed with its type
// parameter dependent (more in the module ExactClause2).
module Generic {
  function Id<X>(x: X): X { x }
  datatype T = T(f: int) {
    invariant Id(this).f >= 0  // accepted: Id does not refer to the set of values of X
  }
  datatype Independent = Independent(f: int) {
    invariant Id(f) >= 0  // accepted
  }
}

// Clause 3: an applied function value other than a lambda expression or a named function.
module FunctionValues {
  datatype Stored = Stored(f: int, g: int -> bool) {
    invariant g(f)  // error
  }
  datatype StoredRequires = StoredRequires(f: int, g: int --> bool) {
    invariant g.requires(f)  // error
  }
  function Pos(x: int): bool { x > 0 }
  datatype OnTheSpot = OnTheSpot(f: int) {
    invariant ((x: int) => x + 1)(f) > 0 && Pos(f)  // accepted
  }
}

// Clause 4: a dynamically bound function.  An override may test a constructor application, which is
// raw, against T, as Teller.Ok does, so the call is rejected even where T involves references.
module Dynamic {
  trait Oracle extends object {
    ghost predicate Ok(n: int)
  }
  datatype Liar3 = Liar3(f: int, o: Oracle) {
    invariant o.Ok(f)  // error
  }
  class Teller extends Oracle {
    ghost predicate Ok(n: int) { !(Liar3(n, this) is Liar3) }
  }

  // The class example of section 6.1, rejected for the same reason.
  trait Shape extends object { predicate Fits(n: int) }
  class Box {
    const s: Shape
    const n: int
    invariant s.Fits(n)  // error
    constructor (s: Shape, n: int) requires s.Fits(n) { this.s := s; this.n := n; }
  }
}

// Clause 5: a test or conversion of a raw value to a type that depends on T.  Inside an invariant,
// 'this' is raw, and so is a constructor application.
module RawValues {
  datatype Argument = Argument(f: int) {
    ghost predicate Q(t: Argument) { t.f == 0 }
    invariant f == 0 ==> Q(this)  // error
  }
  datatype Conversion = Conversion(f: int) {
    invariant f == 0 ==> (this as Conversion).f == 0  // error
  }
  datatype Let = Let(f: int) {
    invariant var t: Let := this; t.f == 0  // error
  }
  datatype Test = Test(f: int) {
    invariant f == 0 ==> !(Test(1) is Test)  // error
  }
  datatype Box = Box(t: Component)
  datatype Component = Component(f: int) {
    invariant f == 0 ==> Box(this).t.f == 0  // error
  }
  function H(t: Receiver): int { t.f }
  datatype Receiver = Receiver(f: int) {
    function G(): int { H(this) }  // error: 'this' is raw, since the invariant applies G to it
    invariant G() >= 0
  }
  datatype Tree = Leaf(key: int) | Node(key: int, kids: Forest) {
    invariant Forest.Nil.Count() == 0  // error: Count is not raw, so Nil is converted to Forest
  }
  datatype Forest = Nil | Cons(head: Tree, tail: Forest) {
    invariant Cons? ==> tail.Count() < 100
    function Count(): nat { if Nil? then 0 else 1 + tail.Count() }
  }
  datatype Sub = Node(f: int, kids: seq<Sub>) {
    invariant forall k <- kids :: k is Small ==> k.f >= 0  // error: k is inferred to be a 'raw Sub'
  }
  type Small = t: Sub | t.f < 10 witness *

  // accepted: an upcast to the raw type is no test, nor is a test of a finished value
  datatype Upcast = Upcast(f: int) {
    ghost predicate R(t: raw Upcast) { t.f >= 0 }
    invariant R(this)
  }
  datatype Finished = Node(f: int, kids: seq<Finished>) {
    invariant forall k: Finished <- kids :: k is Small2 ==> k.f >= 0
  }
  type Small2 = t: Finished | t.f < 10 witness *
}

// The invariant of another type is in the cone only where it depends on T.
module Others {
  const Limit := 10
  datatype U = U(x: int) { invariant x > 0 }
  datatype T = T(u: U) { invariant u.x > 1 && u.x < Limit }
}

// Generic datatypes.  A type with invariants reached in the cone is analysed with the dependence of each
// of its type parameters, as a generic subset type is, and so is a generic member function (clause 2).
module Generics {
  datatype R1<X(!new)> = R1(n: int) {
    invariant forall r: R1<X> :: r.n == 0  // error: clause 1
  }
  datatype R2<X> = R2(n: int) {
    invariant forall r: R2<int> :: r.n == 0  // error: clause 1, at another instantiation
  }
  datatype R3<X(!new)> = R3(n: int) {
    invariant forall r: raw R3<X> :: r.n == 0  // error: clause 1 (counting raw R3 is conservative, section 3.3)
  }
  datatype R4<X(!new)> = R4(n: int, x: X) {
    invariant n > 0
  }
  datatype R5<X(!new)> = R5(r: R4<X>) {
    invariant forall q: R4<X> :: q.n > 0  // accepted: R4 does not depend on R5
  }
  datatype R6<X(!new)> = R6(n: int) {
    invariant (R6(1) as raw R6<X>) is R6<X>  // error: clause 5
  }
  datatype Q<X(!new)> = Q(n: int) {
    invariant forall r: R4<Q<X>> :: r.n == 0  // error: clause 1, through a type argument
  }

  // A component of a generic type with invariants, instantiated with the type itself.
  datatype Box<X> = Box(x: X) {
    invariant true
  }
  datatype T = T(b: Box<T>) | Leaf {
    invariant true
  }
  type Sub<X> = b: Box<X> | true
  datatype U = U(s: Sub<U>) | ULeaf {
    invariant true
  }
  datatype L<X> = Nil | Cons(x: X, t: L<X>) {
    invariant Len() < 100  // accepted where V instantiates L: Len does not refer to the set of values of X
    function Len(): nat { if Nil? then 0 else 1 + t.Len() }
  }
  datatype V = V(l: L<V>) | VLeaf {
    invariant true
  }

  // V5c of git-issue-105-vacuity.dfy: the invariant of P<Liar3> refers to the set of values of Liar3.
  ghost predicate Yes<X>(x: X) { true }
  datatype P<X(!new)> = P(b: bool) {
    invariant b <==> forall x: X :: !Yes(x)  // error: clause 1, where Liar3 instantiates P
  }
  datatype Liar3 = Liar3(p: P<Liar3>) {
    invariant p.b
  }
}

// Clause 2 (stage 7 of section 7.2 of the plan): a generic function or constant given a type argument that
// depends on T is analysed with that type parameter dependent, so the other clauses are checked in its
// specification and body as instantiated, and in what they reach in turn.
module ExactClause2 {
  ghost predicate All<X(!new)>(x: X) { forall y: X :: y == x }  // error: clause 1, where Direct instantiates All
  datatype Direct = Direct(f: int) {
    invariant All(this)
  }

  ghost predicate Inner<X(!new)>(x: X) { exists y: X :: y != x }  // error: clause 1, where Outer instantiates Inner
  ghost predicate Outer<Y(!new)>(y: Y) { Inner(y) }
  datatype Transitive = Transitive(f: int) {
    invariant Outer(this)
  }

  ghost function {:axiom} Pick<X(!new)>(x: X): X ensures forall y: X :: y == x  // error: clause 1, in a specification

  datatype Specified = Specified(f: int) {
    invariant Pick(this) == this
  }

  ghost predicate Arrow<X>(x: X) { var f: X -> bool := y => true; f == f }  // error: the arrow type's constraint
  datatype ThroughArrow = ThroughArrow(f: int) {
    invariant Arrow(this)
  }

  ghost predicate Chosen<X(!new)>(x: X) { var y: X :| true; y == x }  // error: clause 1, a let-such-that
  datatype ThroughChoice = ThroughChoice(f: int) {
    invariant Chosen(this)
  }

  // A function reached at two instantiations is analysed at each.
  ghost predicate Both<X(!new)>(x: X) { forall y: X :: y == x }  // error: clause 1, where Twice instantiates Both
  datatype Twice = Twice(f: int) {
    invariant Both(f) && Both(this)
  }

  // A constant of a generic type: V5d of git-issue-105-vacuity.dfy.  Before stage 7 the cone reached the
  // constant without the dependence of its type argument, so the quantifier went unchecked.
  datatype Box<X(!new)> = Box(x: X) {
    ghost const AllEqual: bool := forall y: X :: y == x  // error: clause 1, where Liar instantiates Box
    ghost const Same: bool := x == x
    function Get(): X { x }
    ghost predicate Other() { exists y: X :: y != x }  // error: clause 1, where ThroughMember instantiates Box
  }
  datatype Liar = Liar(b: Box<Liar>) | Leaf {
    invariant this.Liar? ==> b.AllEqual
  }
  datatype ThroughMember = ThroughMember(b: Box<ThroughMember>) | TLeaf {
    invariant this.ThroughMember? ==> b.Other()
  }

  // Accepted: the callees use their parameter only through values and bounded variables.
  ghost predicate AllIn<X>(xs: set<X>, ys: set<X>) { forall y <- xs :: y in ys }
  datatype Sets = Sets(kids: set<Sets>, more: set<Sets>) {
    invariant AllIn(kids, more)
  }
  datatype Member = Member(b: Box<Member>) | MLeaf {
    invariant this.Member? ==> b.Same && b.Get().MLeaf?
  }
}
