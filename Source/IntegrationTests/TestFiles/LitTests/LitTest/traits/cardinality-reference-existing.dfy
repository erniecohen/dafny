// RUN: %exits-with 2 %resolve "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// These definitions also exercise the existing local cardinality rule.
// Their rejection alone is not evidence that the new global checker ran.
module NonNullWrapper {
  class G<!X> {
    const p: X -> bool
    constructor(p: X -> bool) { this.p := p; }
  }
  datatype D = Ground | Wrap(g: G<D>)
}
module NullableWrapper {
  class G<!X> {
    const p: X -> bool
    constructor(p: X -> bool) { this.p := p; }
  }
  datatype D = Ground | Wrap(g: G?<D>)
}
