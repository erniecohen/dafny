// The entry point belongs to a group, with value-type trait conversions.
module Entry {
  import Peer
  trait Value { function Get(): int }
  newtype N extends Peer.Value = n: int | 0 <= n < 10 {
    function Get(): int { this as int }
  }
  class Other extends Value {
    constructor () { }
    function Get(): int { 7 }
  }
  datatype T = End | More(t: Peer.T)
  method Main() {
    var n: N := 3;
    var value: Peer.Value := n;
    print value.Get(), " ", value is N, " ", (value as N) as int, "\n";
    var d := T.More(Peer.T.End);
    print d, "\n";
    var item: Value := new Peer.Item();
    var other: Value := new Other();
    print item.Get(), " ", item is Peer.Child, " ", other is Peer.Child, "\n";
  }
}
module Peer {
  import Entry
  trait Value { function Get(): int }
  trait Child extends Entry.Value { }
  class Item extends Child {
    constructor () { }
    function Get(): int { 8 }
  }
  datatype T = End | More(t: Entry.T)
}
