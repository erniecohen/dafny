// RUN: %exits-with 2 %baredafny resolve --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Unrun isolated hidden operation must fail during resolution.

module Provider {
  newtype Base<T> = ORDINAL
  newtype Exposed<U> = Base<seq<U>>
  export API provides Base reveals Exposed
}

module Client {
  import P = Provider`API
  lemma CannotOrder(a: P.Exposed<int>, b: P.Exposed<int>) {
    var ordered := a < b; // ERROR: hidden carrier does not expose ordinal ordering
  }
}
