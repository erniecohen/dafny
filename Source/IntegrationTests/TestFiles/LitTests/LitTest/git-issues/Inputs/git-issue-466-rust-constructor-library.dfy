module Constructed refines ImportedBase {
  class {:extern "Store"} Store {
    constructor (seed: int)
      ensures Read() == seed + Tag() + 1
    { new; Write(seed + Tag()); Write(Read() + 1); }
    function {:extern} Tag(): int
    function {:extern} Read(): int reads this
    method {:extern} Write(value: int)
      modifies this
      ensures Read() == value
  }
  class {:extern "Cell"} Cell<T> {
    constructor Init(seed: T)
      ensures Read() == seed
    { new; Write(seed); }
    function {:extern} Read(): T reads this
    method {:extern} Write(value: T)
      modifies this
      ensures Read() == value
  }
}
abstract module AbstractConstructed refines Constructed { }
module ConstructedAgain refines AbstractConstructed { }
