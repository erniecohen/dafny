// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module Provider {
  codatatype Stream = Cons(head: int, tail: Stream)
  newtype Wrapped = Stream witness *
  export Public provides Wrapped
  export BaseHidden provides Stream reveals Wrapped
}

module VisibleNewtypeOpaqueBase {
  import P = Provider`BaseHidden
  lemma Hidden(x: P.Wrapped) {
    var h := x.head;
    var t := (x as P.Stream).tail;
    var y := x ==#[1] x;
  }
}

module Consumer {
  import P = Provider`Public
  lemma Hidden(x: P.Wrapped) {
    assert x.Cons?;
    var h := x.head;
    var y := x ==#[1] x;
  }
}
