module {:extern "Imported"} Base {
  import Peer
  trait Marker { }
  class {:extern "Native"} Native  {
    constructor {:extern} ()

  }
}
module Peer { import Base }
module Refined refines Base { class Native ... { function Added(): int { 0 } } }
