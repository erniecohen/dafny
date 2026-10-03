// RUN: %exits-with 2 %resolve "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module Erased { trait V {} datatype D<T> extends V = D(x: T) }
module NotNew { trait V {} datatype D<T(!new)> extends V = D(x: T) }
module IntermediateTrait { trait V {} trait Middle<T> extends V {} }
module GhostOnly { trait V {} datatype D<T> extends V = D(ghost x: T) }
module Phantom { trait V {} datatype D<T> extends V = D }
module NestedIndex { trait V<T> {} datatype D<X> extends V<seq<X>> = D(x: X) }
module ParentContract { trait V<T> {} datatype D<!T> extends V<T> = D(p: T -> bool) }
module DiamondErasure {
  trait Root<T> {}
  trait Retaining<T> extends Root<T> {}
  trait Erasing<T> extends Root<int> {}
}
module EqualityCharacteristic { trait V {} datatype D<T(==)> extends V = D(x: T) }
module BoundedFormal { trait V {} trait Bound {} datatype D<T extends Bound> extends V = D(x: T) }

module NonReferenceUnusedSubtypeFamily {
  trait T<X> { }
  class D<X1> extends T<(int, seq<X1>)> { }
  type F<X2, Unused> = d: D<X2> | true witness *
}
