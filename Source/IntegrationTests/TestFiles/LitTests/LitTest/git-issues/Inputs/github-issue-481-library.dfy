// Part of github-issue-481.dfy: a library whose export hides bodies and fields.
module Lib {
  export provides Ref.Set, Ref.Get, Make, Tr, Tr.Put reveals Ref
  class Ref<T> {
    ghost var w: T
    constructor (ghost w: T) { this.w := w; }
    method Set(ghost t: T) modifies this { w := t; }
    ghost function Get(): T reads this { w }
  }
  method Make<T>(ghost t: T) returns (r: Ref<T>) { r := new Ref(t); }
  trait Tr {
    method Put<U(!reads)>(a: array<U>, u: U) modifies a requires a.Length > 0
  }
}
module LibOpaque {
  export provides Ref, Make
  class Ref<T> {
    ghost var w: T
    constructor (ghost w: T) { this.w := w; }
  }
  method Make<T>(ghost t: T) returns (r: Ref<T>) { r := new Ref(t); }
}
