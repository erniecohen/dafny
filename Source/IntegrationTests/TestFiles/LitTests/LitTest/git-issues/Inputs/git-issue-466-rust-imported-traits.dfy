module {:extern "Imported"} Base {
  import Peer
  trait Marker { }
  class {:extern "Native"} Native extends Marker {
    constructor {:extern} ()

  }
}
module Peer { import Base }
module Refined refines Base {  }
