// RUN: %exits-with 2 %baredafny resolve --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Unrun isolated visibility control: forbidden operation must fail.

module Provider {
  newtype Base<T> = o: ORDINAL | o != 0 witness 1
  newtype Exposed<U> = Base<seq<U>>
  export API provides Base reveals Exposed
}

module Client {
  import P = Provider`API
  lemma CannotObserve(o: P.Exposed<int>) {
    var raw := o as ORDINAL; // ERROR: hidden base operation
  }
}
