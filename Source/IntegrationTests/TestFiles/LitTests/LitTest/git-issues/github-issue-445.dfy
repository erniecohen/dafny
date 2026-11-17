// RUN: %exits-with 2 %resolve --type-system-refresh:false --general-traits:legacy --general-newtypes:false "%s" > "%t"
// RUN: %exits-with 2 %resolve "%s" >> "%t"
// RUN: %run --target cs --type-system-refresh:false --general-traits:legacy --general-newtypes:false "%S/Inputs/github-issue-445-run.dfy" >> "%t"
// RUN: %run --target cs "%S/Inputs/github-issue-445-run.dfy" >> "%t"
// RUN: %run --target cs --legacy-early-yield-parameter-check "%S/Inputs/github-issue-445-legacy.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/445: the resolver checked that the type of each yield
// parameter supports auto-initialization while it was still resolving the signatures of the module's
// declarations.  At that point:
// - no datatype has a grounding constructor yet, so Type.GetAutoInit followed another constructor.
//   That constructor could be recursive, so a datatype with values was rejected, or, before the fix
//   for https://github.com/erniecohen/dafny/issues/413, overflowed the stack.  It could also have a
//   compilable value where the grounding constructor, which the compiler uses for the default value,
//   has none, so a verified program could read a yield parameter that the compiled code left null;
// - the constructors of a datatype declared after the iterator have unresolved parameter types, so
//   the resolver crashed.
// The check now runs once the grounding constructors are known.  Inputs/github-issue-445-run.dfy
// compiles and runs an iterator whose yield type has a recursive first constructor.
// Inputs/github-issue-445-legacy.dfy shows the old check, which the hidden
// --legacy-early-yield-parameter-check restores, accepting a yield type the compiler cannot initialize.

// Each type here has a compilable value, so each iterator is accepted.
module Accepted {
  // the first constructor is recursive
  datatype L = Cons(int, L) | Nil
  // the first constructor is recursive, with a type that doubles at each step
  datatype Nest<A> = Cons(A, Nest<(A, A)>) | Nil
  // the first constructor leads back through another datatype
  datatype A = A1(b: B) | A0
  datatype B = B(a: A)
  // the first constructor has a type parameter, which need not support auto-initialization
  datatype P<T> = Cons(T, P<T>) | Nil
  iterator I() yields (l: L, n: Nest<int>, b: B, ghost g: L) { }
  iterator J<T>() yields (p: P<T>) { }
}

// The iterator is declared before the datatypes of its yield parameters.
module DeclaredLater {
  iterator I() yields (w: W, l: L) { }
  datatype W = W(b: B)
  datatype B = B(i: int)
  datatype L = Cons(int, L) | Nil
}

// The iterator is refined in another module, which declares it before the datatype it inherits.
abstract module Abstract {
  datatype L = Cons(int, L) | Nil
  iterator I() yields (l: L)
}
module Refined refines Abstract {
  iterator I ... { }
}

// Each type here may be empty, or has no compilable value, so each yield parameter is still rejected.
module Rejected {
  iterator Later() yields (w: W) { } // error
  datatype W = W(e: E)
  datatype E = E(e: E)
  trait Tr { }
  type Ghosty = x | x % 2 == 1 ghost witness 77
  iterator I() yields (e: E, t: Tr, h: Ghosty) { } // error (3)
  iterator J() yields (ghost e: E, ghost t: Tr, ghost h: Ghosty) { } // error (2)
  iterator K<T>() yields (x: T) { } // error
  // The first constructor, C1, has a compilable value, but the grounding constructor, C2, which the
  // compiler uses for the default value, has none, and it may be empty as far as the resolver can tell.
  // A field of type D<int> is rejected for the same reason.
  type S = x: int | x > 0 witness *
  datatype D<T> = C1(t: T) | C2(s: S)
  iterator G() yields (x: D<int>) { } // error
  iterator H() yields (ghost x: D<int>) { } // error
}
