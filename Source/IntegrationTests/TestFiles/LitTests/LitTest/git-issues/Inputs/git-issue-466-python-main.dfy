// Each member needs a distinct replacement declared after the group.
replaceable module SpecOne {
  function Value(): int
}
replaceable module SpecTwo {
  function Value(): int
}
module Zulu {
  import Alpha
  import SpecTwo
  trait Value { function Get(): int }
  class Item extends Alpha.Value {
    constructor () { }
    function Get(): int { SpecTwo.Value() }
  }
}
module Alpha {
  import Zulu
  import SpecOne
  trait Value { function Get(): int }
  class Item extends Zulu.Value {
    constructor () { }
    function Get(): int { SpecOne.Value() }
  }
  method Main() {
    var a: Zulu.Value := new Item();
    var z: Value := new Zulu.Item();
    print a.Get(), " ", z.Get(), "\n";
  }
}
module ZzzOne replaces SpecOne {
  function Value(): int { 17 }
}
module ZzzTwo replaces SpecTwo {
  function Value(): int { 23 }
}
