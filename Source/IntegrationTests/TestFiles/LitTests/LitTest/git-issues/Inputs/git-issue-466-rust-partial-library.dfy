// Foreign allocation and type identity coexist with generated inherent methods.
module Partial refines Extended {
  class Native ... {
    function Local(): int { Read() + Added(1) + Count(3) }
    method Value(x: int) returns (r: int)
      ensures r == x + 101
    { r := Added(x) + 1; }
    function WithMethod(x: int): int { Added(x) + 1 }
      by method { return Added(x) + 1; }
  }
  class Generic<T> ... {
    function ThroughBox(x: T): (r: T) ensures r == x { Echo(x) }
  }
  class Fresh<T> ... {
    function Through(x: T): (r: T) ensures r == x { Echo(x) }
  }
}
abstract module AbstractPartial refines Partial { }
module PartialAgain refines AbstractPartial {
  class Native ... {
    method Twice(x: int) returns (r: int)
      ensures r == x + 102
    { r := Value(x); r := r + 1; }
  }
}
