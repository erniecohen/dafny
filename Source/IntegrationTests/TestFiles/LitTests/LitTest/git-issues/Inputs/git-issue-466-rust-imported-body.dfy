module {:extern "Imported"} Base {
  import Peer
  trait Marker { }
  class {:extern "Native"} Native  {
    constructor {:extern} ()
    function {:extern "Elsewhere", "Read"} Read(): int { 31 }
  }
}
module Peer { import Base }
module Refined refines Base {  }
