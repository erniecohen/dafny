// A program that --refinement-extends rejects when type characteristics are checked.

// ----- A type argument of the added parent that lacks a required characteristic

module Characteristics0 {
  trait Eq<X> extends object {}
}
module Characteristics1 refines Characteristics0 {
  trait NeedsEq<Y(==)> extends object {}
  trait Eq<X> ... extends NeedsEq<X> {} // error: X does not support equality
}
