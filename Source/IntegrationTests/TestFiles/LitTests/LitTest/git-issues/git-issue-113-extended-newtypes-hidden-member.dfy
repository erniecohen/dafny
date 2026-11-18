// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

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
