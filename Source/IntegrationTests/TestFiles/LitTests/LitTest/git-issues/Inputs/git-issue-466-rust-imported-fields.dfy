module {:extern "Imported"} Base {
  import Peer
  trait Marker { }
  class {:extern "Native"} Native  {
    constructor {:extern} ()
    var {:extern} value: int
  }
}
module Peer { import Base }
module Refined refines Base {  }
