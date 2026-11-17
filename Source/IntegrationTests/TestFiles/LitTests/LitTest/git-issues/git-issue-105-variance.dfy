// RUN: %exits-with 2 %resolve --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105): the variance of generic
// datatypes.  D<X> denotes the values of raw D<X> that satisfy the invariants instantiated with X, so
// it is non-variant in each type parameter, whatever D declares (git-issue-105-generics.dfy shows
// why).  A type whose definition uses D<X> is therefore non-variant in X too, and may use raw D<X>,
// which keeps D's declared variance, instead.  Each type parameter keeps its strictness.

datatype List<+X> = Nil | Cons(x: X, tail: List<X>) { invariant true }  // error: X is not covariant
datatype RawList<+X> = RNil | RCons(x: X, tail: raw RawList<X>) { invariant true }

datatype Box<+X> = Box(x: X) { invariant true }
datatype W<+X> = W(b: Box<X>)  // error: X is not covariant
datatype V<+X> = V(b: raw Box<X>)
datatype S<X> = S(b: Box<X>)

datatype Arrow<*X> = Arrow(f: int -> X) { invariant true }
datatype U<*X> = U(a: Arrow<X>)  // error: X is not covariant
datatype U2<!X> = U2(a: Arrow<X>)
datatype T<X> = T(a: Arrow<X>)  // error: X is used left of an arrow, as it would be without the invariant
