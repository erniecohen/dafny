module {:extern "Foreign"} A {
  import B
  class {:extern "Actual"} Native {
    constructor {:extern} ()
    function Local(): int { B.Value() }
    var value: int
  }
}
module B { import A function Value(): int { 1 } }
module Refined refines A {}
