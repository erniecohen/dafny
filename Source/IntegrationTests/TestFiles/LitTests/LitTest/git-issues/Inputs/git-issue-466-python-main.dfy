// A main entry point in a group, with an unrelated replacement below the group.
replaceable module Spec {
  function Value(): int
}
module Impl replaces Spec {
  function Value(): int { 17 }
}
module Zulu {
  import Alpha
  trait Value { function Get(): int }
  class Item extends Alpha.Value {
    constructor () { }
    function Get(): int { 23 }
  }
}
module Alpha {
  import Zulu
  import Spec
  trait Value { function Get(): int }
  class Item extends Zulu.Value {
    constructor () { }
    function Get(): int { Spec.Value() }
  }
  method Main() {
    var a: Zulu.Value := new Item();
    var z: Value := new Zulu.Item();
    print a.Get(), " ", z.Get(), "\n";
  }
}
