// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

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
