// RUN: %exits-with 2 %resolve --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105): the variance of generic
// datatypes (plan section 3.5).  D<X> denotes the values of raw D<X> that satisfy the invariants
// instantiated with X, and has the variance that D declares.  So the invariants must be monotone in
// each covariant type parameter (true at X, they stay true at a larger type) and antitone in each
// contravariant one, or D<nat> could be used as a D<int> that does not satisfy the invariants at int.
// raw D has the declared variance in any case.  Each type parameter keeps its strictness.
// Universal quantification over X is negative in X; existential quantification and a type test
// against X are positive; a negation, the antecedent of an implication and an arrow's parameter
// type flip the polarity; an equivalence, an equality, the condition of a conditional, a
// comprehension and a non-variant type parameter make it mixed; a generic function contributes the
// polarity of its type parameters in its body; a function whose body is not known is mixed.

module Lib {
  export reveals Open provides Hidden
  ghost predicate Open<Y(!new)>() { exists y: Y :: true }
  ghost predicate Hidden<Y(!new)>() { exists y: Y :: true }
}

// Each declaration here resolves only because D<X> has D's declared variance.
module Accepted {
  import Lib
  datatype List<+X> = Nil | Cons(x: X, tail: List<X>) { invariant true }
  datatype RawList<+X> = RNil | RCons(x: X, tail: raw RawList<X>) { invariant true }
  datatype Box<+X> = Box(x: X) { invariant true }
  datatype W<+X> = W(b: Box<X>)
  datatype V<+X> = V(b: raw Box<X>)
  datatype S<X> = S(b: Box<X>)
  datatype Arrow<*X> = Arrow(f: int -> X) { invariant true }
  datatype U<*X> = U(a: Arrow<X>)
  datatype U2<!X> = U2(a: Arrow<X>)

  trait Tr { }
  ghost predicate F<X(!new)>(y: X) { false }
  ghost predicate Some<Y(!new)>() { exists y: Y :: true }
  ghost predicate Rec<Y(!new)>(n: nat) { n == 0 || ((exists y: Y :: true) && Rec<Y>(n - 1)) }
  ghost predicate Unknown<Y>(y: Y)

  datatype Exists<+X(!new)> = Exists(b: bool) { invariant b ==> exists y: X :: true }
  datatype Forall<-X(!new)> = Forall(b: bool) { invariant b ==> forall y: X :: F(y) }
  datatype Antecedent<+X(!new)> = Antecedent(b: bool) { invariant (forall y: X :: F(y)) ==> b }
  datatype Explies<+X(!new)> = Explies(b: bool) { invariant b <== forall y: X :: F(y) }
  datatype Negations<+X(!new)> = Negations(b: bool) { invariant !(b && !(exists y: X :: true)) }
  datatype ArrowForall<+X(!new)> = ArrowForall(b: bool) { invariant b ==> forall f: X -> int :: true }
  datatype ArrowExists<-X(!new)> = ArrowExists(b: bool) { invariant b ==> exists f: X -> int :: true }
  datatype Collection<+X(!new)> = Collection(b: bool) { invariant b ==> exists s: seq<X> :: |s| == 1 }
  datatype TypeTest<+X(!new) extends Tr> = TypeTest(o: Tr) { invariant o is X }
  datatype Values<+X> = Values(x: X, y: X) { invariant x == y }
  datatype Callee<+X(!new)> = Callee(b: bool) { invariant b ==> Some<X>() && Rec<X>(3) }
  datatype Constant<+X(!new)> = Constant(b: bool) {
    ghost const k: bool := exists y: X :: true
    invariant b ==> k
  }
  datatype Revealed<+X(!new)> = Revealed(b: bool) { invariant b ==> Lib.Open<X>() }
  // A function whose body is not known is mixed only in the type parameters it is given.
  datatype Other<+X(!new)> = Other(b: bool, n: int) { invariant b ==> Unknown(n) }
  // A statement in an expression is a proof hint, not part of the invariant.
  datatype Hint<+X(!new)> = Hint(b: bool) { invariant (assert forall y: X :: !F(y); b) }
}

module LeftOfArrow {
  datatype Arrow<*X> = Arrow(f: int -> X) { invariant true }
  datatype T<X> = T(a: Arrow<X>)  // error: X is used left of an arrow, as it would be without the invariant
}

module Rejected {
  import Lib
  trait Tr { }
  type Empty = x: int | false witness *
  ghost predicate F<X(!new)>(y: X) { false }
  ghost predicate All<Y(!new)>() { forall y: Y :: F(y) }
  ghost predicate Bodiless<Y(!new)>()
  ghost predicate RecBad<Y(!new)>(n: nat) { n == 0 || (!(exists y: Y :: true) && RecBad<Y>(n - 1)) }
  datatype G<Y> = G(y: Y)

  // Box(true) is a Box<X> exactly where X is empty, so Box(true) is a Box<Empty>, and if Box were
  // covariant, it would be a Box<int>, and F(0) would hold.
  datatype Box<+X(!new)> = Box(b: bool) { invariant b ==> forall y: X :: F(y) }  // error
  datatype Exists<-X(!new)> = Exists(b: bool) { invariant b ==> exists y: X :: true }  // error
  datatype ArrowExists<+X(!new)> = ArrowExists(b: bool) { invariant b ==> exists f: X -> int :: true }  // error
  datatype ArrowForall<-X(!new)> = ArrowForall(b: bool) { invariant b ==> forall f: X -> int :: true }  // error
  datatype NonVariant<+X(!new)> = NonVariant(b: bool) { invariant b ==> exists g: G<X> :: true }  // error
  datatype Callee<+X(!new)> = Callee(b: bool) { invariant b ==> All<X>() }  // error
  datatype Recursive<+X(!new)> = Recursive(n: nat) { invariant RecBad<X>(n) }  // error
  datatype Opaque<+X(!new)> = Opaque(b: bool) { invariant b ==> Bodiless<X>() }  // error
  datatype Hidden<+X(!new)> = Hidden(b: bool) { invariant b ==> Lib.Hidden<X>() }  // error
  datatype Not<+X(!new)> = Not(b: bool) { invariant !(exists y: X :: true) }  // error
  datatype Iff<+X(!new)> = Iff(b: bool) { invariant b <==> exists y: X :: true }  // error
  datatype Ite<+X(!new)> = Ite(b: bool) { invariant if exists y: X :: true then b else !b }  // error
  datatype Comprehension<+X(!new)> = Comprehension(b: bool) { invariant b ==> (iset y: X | F(y)) == iset{} }  // error
  datatype TypeTest<-X(!new) extends Tr> = TypeTest(o: Tr) { invariant o is X }  // error
  datatype Constant<+X(!new)> = Constant(b: bool) {  // error
    ghost const k: bool := forall y: X :: F(y)
    invariant b ==> k
  }
  datatype Used<+X(!new)> = Used(b: bool) { invariant b ==> var f := F<X>; f == F<X> }  // error
  datatype Contra<-X(!new)> = Contra(b: bool) { invariant b ==> exists y: X :: true }  // error
}

// Issue 105, stage 7 of section 7.2 of the plan: a bound variable that ranges over the values of an
// expression of its own type (the elements of a collection, one value, or the subsets of a set) does not
// use the type, since it ranges over the same values whatever the type argument is.  The types must be the
// same, constraints included, and a let-such-that expression is not covered, since its choice may depend on
// the type argument.
module BoundedVariables {
  ghost predicate F<X(!new)>(y: X) { false }
  type NonEmpty<X> = s: set<X> | |s| > 0 witness *

  datatype Elements<+X(!new)> = Elements(xs: set<X>, ys: set<X>) { invariant forall y <- xs :: y in ys }
  datatype SetOf<+X(!new)> = SetOf(xs: set<X>, ys: set<X>) { invariant (set y <- xs | y in ys) == xs }
  datatype MapOf<+X(!new)> = MapOf(xs: set<X>) { invariant (map y <- xs :: 0) == map[] ==> xs == {} }
  datatype Keys<+X(!new)> = Keys(m: map<X, int>) { invariant forall k <- m :: m[k] > 0 }
  datatype Exact<+X(!new)> = Exact(x: X, ys: set<X>) { invariant forall y: X | y == x :: y in ys }
  datatype Subsets<+X(!new)> = Subsets(xs: set<X>) { invariant forall s: set<X> | s <= xs :: |s| <= |xs| }

  datatype Unbounded<+X(!new)> = Unbounded(xs: set<X>) { invariant forall y: X :: y in xs }  // error
  datatype Constrained<+X(!new)> = Constrained(xss: set<set<X>>) { invariant forall s: NonEmpty<X> | s in xss :: F(s) }  // error
  datatype Choice<+X(!new)> = Choice(xs: set<X>) { invariant xs != {} ==> var y: X :| y in xs; !F(y) }  // error
}
