// RUN: %exits-with 2 %resolve "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module StoredFunction {
  class G<X> {
    const p: X -> bool
    constructor(p: X -> bool) { this.p := p; }
  }
}
module GhostStorage { class G<X> { ghost var p: X -> bool } }
module ReferenceTrait { trait G<X> extends object { ghost var p: X -> bool } }
module IteratorStorage { iterator G<X>(p: X -> bool) {} }
module ReferenceParentContract {
  trait Parent<X> extends object {}
  class Child<!X> extends Parent<X> { ghost var p: X -> bool }
}
