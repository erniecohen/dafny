module Bodied refines Traitful {
  class {:extern "Actual"} Store {
    constructor {:extern "Seed"} (seed: int)
      ensures Read() == seed + Tag() + 1
    { new; Write(seed + Tag()); Write(Read() + 1); }
    function {:extern} Tag(): int
    function {:extern "Current"} Read(): int reads this
    method {:extern "Assign"} Write(value: int)
      modifies this
      ensures Read() == value
    function {:extern "Next"} Next(value: int): int { value + 1 }
    method {:extern "Advance"} Advance(value: int) returns (r: int)
      ensures r == value + 1
    { print "advance\n"; r := Next(value); }
    function {:extern "Identity"} Echo<U>(value: U): (r: U)
      ensures r == value
    { value }
  }
  class {:extern "ActualCell"} Cell<T> {
    constructor {:extern "Init"} Init(seed: T)
      ensures Read() == seed
    { new; Write(seed); }
    function {:extern} Read(): T reads this
    method {:extern} Write(value: T)
      modifies this
      ensures Read() == value
    function {:extern "RoundTrip"} Echo(value: T): (r: T)
      ensures r == value
    { value } by method { print "body echo\n"; return value; }
  }
}
abstract module AbstractBodied refines Bodied { }
module BodiedAgain refines AbstractBodied { }
