// RUN: %exits-with 2 %resolve --type-system-refresh:false --general-traits:legacy --general-newtypes:false "%s" > "%t"
// RUN: %exits-with 2 %resolve "%s" >> "%t"
// RUN: %exits-with 4 %verify --type-system-refresh:false --general-traits:legacy --general-newtypes:false "%S/Inputs/github-issue-413-locals.dfy" >> "%t"
// RUN: %exits-with 4 %verify "%S/Inputs/github-issue-413-locals.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/413: asking whether a recursive datatype with no
// values is nonempty overflowed the stack in Type.GetAutoInit.  It follows the grounding
// constructor of each datatype, and a datatype with no values has none, so it fell back to a
// constructor that leads back to the datatype, forever.  Each module below crashed; now each is
// an ordinary error.  Inputs/github-issue-413-locals.dfy has the locals of such a type, which
// crashed the verifier.

// The issue's first program: such a datatype, for a type parameter that must be nonempty.
module TypeArgument {
  datatype D = K(t: D)
  method M<X(00)>() { }
  method Use() { M<D>(); }
}

// The issue's second program: a type declared nonempty, refined by such a datatype.
abstract module A {
  type T(00)
  datatype D = K(t: T)
}
module B refines A {
  type T = D
}

// Through another datatype.
module Mutual {
  datatype A = A(b: B)
  datatype B = B(a: A)
  method M<X(00)>() { }
  method Use() { M<A>(); }
}

// Through a total arrow.
module Arrow {
  datatype F = F(f: int -> F)
  method M<X(00)>() { }
  method Use() { M<F>(); }
}

// Non-regular recursion: the type grows, P<int>, P<seq<int>>, P<seq<seq<int>>>, ...
module NonRegular {
  datatype P<X> = P(p: P<seq<X>>)
  method M<X(00)>() { }
  method Use() { M<P<int>>(); }
}

// Controls, which keep their answers: each type here has a value.
module Controls {
  // the first constructor is recursive
  datatype L = Cons(int, L) | Nil
  // non-regular, with a value
  datatype Nest<A> = Cons(A, Nest<(A, A)>) | Nil
  // met again with a smaller type
  datatype G<X> = G(x: X)
  // no grounding constructor, but a value through the codatatype
  codatatype C<T> = C(c: C<T>)
  datatype E<T> = E(c: C<T>)
  datatype R = R(e: E<R>)
  // no grounding constructor, for the same reason.  W is met again with a larger type, W<Big<V>>,
  // but only after the recursion went into the type argument of W<V>, so the recursion does not repeat
  datatype W<T> = W(a: T, e: E<W<T>>)
  datatype Big<A> = Big(b: int)
  datatype V = V(w: W<Big<V>>)
  method M<X(00)>() { }
  method Use() {
    M<L>();
    M<Nest<int>>();
    M<G<G<int>>>();
    M<R>();
    M<W<V>>();
  }
}
