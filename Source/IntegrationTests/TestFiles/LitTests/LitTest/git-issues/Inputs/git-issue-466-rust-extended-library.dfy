// Every executable member remains foreign; refinement changes the foreign API.
module Extended refines RefinedImported {
  class Native ... {
    function {:extern "Extra"} Added(x: int): (r: int) ensures r == x + 100
    ghost function Marker(): int { 0 }
  }
  class {:extern "Fresh"} Fresh<T> {
    constructor {:extern} ()
    function {:extern} Echo(x: T): (r: T) ensures r == x
  }
}
abstract module AbstractExtended refines Extended { }
module ExtendedAgain refines AbstractExtended {
  class Native ... {
    function {:extern} Second(x: int): (r: int) ensures r == x + 200
  }
}
