// RUN: %exits-with 2 %baredafny resolve --type-system-refresh:false --use-basename-for-filename --show-snippets:false --standard-libraries:false "%s" > "%t"
// RUN: %exits-with 2 %baredafny resolve --type-system-refresh --use-basename-for-filename --show-snippets:false --standard-libraries:false "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Selecting a member of a newtype whose base is not numeric used to crash the legacy resolver
// (https://github.com/erniecohen/dafny/issues/417). Each resolver now reports that the newtype must be
// based on a numeric type, and that the base's members are not members of the newtype.

module ArrowBase {
  newtype Partial = int --> int witness ((x: int) => x)

  method M(f: Partial, x: int)
    requires f.requires(x)
  {
  }
}

module DatatypeBase {
  datatype A = A(n: int) {
    function Get(): int { n }
  }
  newtype N = A witness A(0)

  method M(a: N) {
    var k := a.Get();
    var m := a.n;
  }
}

// The base of Deep is a newtype whose base is a generic datatype.
module GenericChain {
  datatype Pair<A, B> = Pair(left: A, right: B)
  newtype Flip<X, Y> = p: Pair<Y, X> | true witness *
  newtype Deep<T> = p: Flip<seq<T>, bool> | true witness *

  method M(d: Deep<int>) {
    var left: bool := d.left;
  }
}

// The legacy resolver did not crash here, but accepted n.F().
module ClassBase {
  class C {
    function F(): int { 0 }
  }
  newtype N = C? witness null

  method M(n: N) returns (k: int) {
    k := n.F();
  }
}

// A numeric base still gives the newtype its members, whether the base is written out or inferred.
module NumericBase {
  newtype Count = int {
    function Next(): Count { this + 1 }
  }
  newtype Few = c: Count | c < 3

  newtype Small = x | 0 <= x < 10 {
    function Twice(): int { this as int * 2 }
  }
  newtype Smaller = s: Small | s < 5

  method M(f: Few, s: Smaller) returns (n: Count, r: int) {
    n := f.Next();
    r := s.Twice();
  }
}
