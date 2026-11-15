// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module Provider {
  newtype Ord = ORDINAL
  export Visible reveals Ord
  export Hidden provides Ord
}

module VisibleClient {
  import P = Provider`Visible
  lemma Observes(o: P.Ord) {
    var base := o as ORDINAL;
    assert o.Offset == base.Offset;
  }
}

module HiddenClient {
  import P = Provider`Hidden
  lemma CannotObserve(o: P.Ord) {
    var base := o as ORDINAL; // ERROR: hidden representation is unavailable
    var offset := o.Offset; // ERROR: hidden ordinal members are unavailable
  }
}

module HiddenBaseProvider {
  newtype Base<T> = o: ORDINAL | o != 0 witness 1
  newtype Exposed<U> = Base<seq<U>>
  export API provides Base reveals Exposed
}

module HiddenBaseClient {
  import P = HiddenBaseProvider`API
  lemma CannotTraverse(o: P.Exposed<int>) {
    var base := o as P.Base<seq<int>>;
    var raw := o as ORDINAL; // ERROR: revealed outer type cannot reveal hidden Base
    var offset := o.Offset; // ERROR: operation view stops at hidden Base
  }
}
