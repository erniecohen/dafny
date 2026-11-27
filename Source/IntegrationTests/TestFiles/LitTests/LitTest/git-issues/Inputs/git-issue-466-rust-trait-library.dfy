module Traitful refines ImportedBase {
  trait Marker {
    ghost var memo: int
    function BaseTag(): int { 157 }
  }
  trait Protocol<T> extends Marker {
    function Get(x: T): (r: T) ensures r == x
    function Tag(): int
  }
  class {:extern "Generated"} Generated<T> extends Protocol<T> {
    constructor {:extern} ()
    function {:extern} Echo(x: T): (r: T) ensures r == x
    function {:extern} ForeignTag(): int
    function Get(x: T): (r: T) ensures r == x { Echo(x) }
    function Tag(): int { ForeignTag() }
  }
  class {:extern "Foreign"} Foreign<T> extends Protocol<T> {
    constructor {:extern} ()
    function {:extern} Get(x: T): (r: T) ensures r == x
    function {:extern} Tag(): int
  }
}
abstract module AbstractTraitful refines Traitful { }
module TraitfulAgain refines AbstractTraitful { }
