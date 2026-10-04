// RUN: %exits-with 2 %resolve --type-system-refresh=true --general-newtypes --extended-newtype-bases "%s" > "%t"
// Unexecuted draft: an operation view cannot reveal an exported opaque base.

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
