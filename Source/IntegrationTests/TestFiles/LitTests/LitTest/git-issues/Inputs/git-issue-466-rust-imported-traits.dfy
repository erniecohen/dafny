module {:extern "Imported"} Base {
  import Peer
  trait Root { var value: int }
  trait Marker extends Root { }
  class {:extern "Native"} Native extends Marker {
    constructor {:extern} ()
  }
}
module Peer { import Base }
module Refined refines Base { }
