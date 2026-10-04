// RUN: %exits-with 2 %baredafny resolve --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Unrun isolated visibility control: forbidden operation must fail.

module Provider {
  newtype Base<T> = ORDINAL
  newtype Exposed<U> = Base<seq<U>>
  export API provides Base reveals Exposed
}

module Client {
  import P = Provider`API
  lemma CannotIntroduce(o: ORDINAL) {
    var nominal := o as P.Exposed<int>; // ERROR: hidden Base carrier cannot be discovered
  }
}
