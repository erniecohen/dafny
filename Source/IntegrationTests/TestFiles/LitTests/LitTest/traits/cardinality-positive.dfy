// RUN: %verify "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module Basic {
  trait V { function tag(): int }
  datatype D extends V = D(n: int) { function tag(): int { n } }
  predicate Observer(v: V) { v.tag() > 0 }
  function Apply<T>(f: T -> bool, v: T): bool { f(v) }
  method Good() {
    var v: V := D(3);
    assert v is D;
    assert v.tag() == 3;
    assert (v as D).n == 3;
    assert Observer(v);
    assert Apply(Observer, v);
  }
}
module PositiveRecursion {
  trait V {}
  datatype D extends V = Ground | Single(child: V) | Many(many: seq<V>)
    | FiniteSet(ghost elements: set<V>) | FiniteMap(ghost ranges: map<int, V>)
    | DomainMap(ghost domains: map<V, int>) | Multi(ghost occurrences: multiset<V>)
}
module ArrowResult {
  trait V {}
  datatype Leaf extends V = Leaf(n: int)
  datatype D extends V = D(f: int -> V)
}
module InfiniteMapRange {
  trait V {}
  datatype Leaf extends V = Leaf(n: int)
  datatype D extends V = D(m: imap<int, V>)
}
module Retained {
  trait V<T> {}
  datatype D<T> extends V<T> = D(x: T)
  method Good() {
    var v: V<int> := D(3);
    assert v is D<int>;
    assert (v as D<int>).x == 3;
  }
}
module Permutation { trait V<X, Y> {} datatype D<A, B> extends V<B, A> = D(a: A, b: B) }
module ClosedIndex { trait V<X, Y> {} datatype D<A> extends V<A, int> = D(a: A) }
module DirectAndNested { trait V<X, Y> {} datatype D<A> extends V<A, seq<A>> = D(a: A) }
module IdentityRetention { type Id<T> = T trait V<T> {} datatype D<A> extends V<Id<A>> = D(a: A) }
module Permissive { trait V<!X> {} datatype D<!X> extends V<X> = D(f: X -> bool) }
module DuplicateRetention { trait V<X, !Y> {} datatype D<!T> extends V<T, T> = D(f: T -> bool) }
module ReferenceContracts {
  class Ordinary<T> { var x: T constructor(x: T) { this.x := x; } }
  class CallbackMethod<T> { method Apply(f: T -> bool, x: T) returns (b: bool) { b := f(x); } }
  class CallbackField<!T> { ghost var f: T -> bool }
  class SelfReference { ghost var f: SelfReference -> bool }
  trait InheritedCallback<!T> extends object { ghost var f: T -> bool }
  class InheritedField<!T> extends InheritedCallback<T> {}
}
module PositiveDiamond {
  trait Root<T> {}
  trait Left<T> extends Root<T> {}
  trait Right<T> extends Root<T> {}
  datatype D<T> extends Left<T>, Right<T> = D(x: T)
}
module IndependentExpansion { trait V {} datatype D extends V = D(n: int) datatype Observer = Observer(f: V -> bool) }
module First { import S = Second trait V {} datatype D extends V = D(f: S.V -> bool) }
module Second { trait V {} datatype D extends V = D(n: int) }

module ProvidedFactory {
  export provides V, V.Tag, D, Make
  trait V { function Tag(): (r: int) ensures r == 3 }
  datatype D extends V = D(n: int) {
    function Tag(): (r: int) ensures r == 3 { 3 }
  }
  method Make() returns (v: V)
    ensures v.Tag() == 3
  { v := D(3); }
}
module ProvidedClient {
  import S = ProvidedFactory
  method Witness() {
    var v := S.Make();
    assert v.Tag() == 3;
  }
}

module TraitChain {
  trait Root {}
  trait Child extends Root {}
  datatype D extends Child = D(n: int)
  method Good() {
    var v: Root := D(3);
    assert v is Child;
    assert v is D;
    assert (v as D).n == 3;
  }
}
abstract module DatatypeRefinementTemplate {
  trait V {}
  type D extends V
}
module DatatypeRefinement refines DatatypeRefinementTemplate {
  datatype D = D(n: int)
}
module OpenedViewApi { trait {:termination false} V {} }
module OpenedViewStorage {
  import A = OpenedViewApi
  export Opaque provides B, A
  export Transparent provides A reveals B, F
  type F = int
  datatype B = B(f: F)
}
module OpenedViewClient {
  import A = OpenedViewApi
  import opened O = OpenedViewStorage`Opaque
  import opened R = OpenedViewStorage`Transparent
  datatype D extends A.V = D(hidden: O.B, transparent: R.B)
}

module ReplacementApi { trait {:termination false} V {} }
replaceable module ReplacementStorage {
  import A = ReplacementApi
  type F
}
module SelectedStorage replaces ReplacementStorage { type F = int }
module ReplacementClient {
  import A = ReplacementApi
  import S = ReplacementStorage
  datatype D extends A.V = Ground | D(f: S.F)
}

abstract module ReplacementAliasTemplate { type D<!X> }
replaceable module ReplacementAlias refines ReplacementAliasTemplate {}
module SelectedAlias replaces ReplacementAlias { type D<X> = X }
