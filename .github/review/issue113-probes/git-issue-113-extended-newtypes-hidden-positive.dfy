// RUN: %baredafny verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Unrun isolated visibility positives.

module VisibleProvider {
  newtype Ord = ORDINAL
  newtype Layer = Ord
  export API reveals Ord, Layer
}

module VisibleClient {
  import P = VisibleProvider`API
  lemma Observe(o: P.Layer) {
    var raw := o as ORDINAL;
    assert o.Offset == raw.Offset;
    assert o.IsNat == raw.IsNat;
    var wrapped := raw as P.Layer;
    assert (wrapped as ORDINAL) == raw;
  }
}

module OwnProvider {
  newtype Ord = ORDINAL {
    ghost function Identity(): (r: Ord)
      ensures r == this
    { this }
  }
  export API provides Ord, Ord.Identity
}

module OwnClient {
  import P = OwnProvider`API
  lemma UseOwn(o: P.Ord) {
    var same: P.Ord := o.Identity();
    assert same == o;
  }
}

module HiddenBaseProvider {
  newtype Base<T> = ORDINAL {
    ghost function Identity(): (r: Base<T>)
      ensures r == this
    { this }
  }
  newtype Exposed<U> = Base<seq<U>>
  export API provides Base, Base.Identity reveals Exposed
}

module HiddenBaseClient {
  import P = HiddenBaseProvider`API
  lemma ReachOnlyNamedBase(o: P.Exposed<int>) {
    var base := o as P.Base<seq<int>>;
    var same: P.Base<seq<int>> := o.Identity();
    assert same == base;
  }
}
