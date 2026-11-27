module {:extern "Foreign"} A {
  import B
  trait T {
    function F(): int
    function G(): int
  }
  class {:extern} Native extends T {
    constructor {:extern} ()
    function {:extern} F(): int
    function G(): int { B.Value() }
  }
}
module B { import A function Value(): int { 1 } }
