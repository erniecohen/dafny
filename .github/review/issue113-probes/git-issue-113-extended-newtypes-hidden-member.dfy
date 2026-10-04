// RUN: %exits-with 2 %baredafny resolve --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Unrun isolated visibility control: forbidden operation must fail.

module Provider {
  newtype Ord = ORDINAL
  export Hidden provides Ord
}

module Client {
  import P = Provider`Hidden
  lemma CannotObserve(o: P.Ord) {
    var offset := o.Offset; // ERROR: hidden base operation
  }
}
