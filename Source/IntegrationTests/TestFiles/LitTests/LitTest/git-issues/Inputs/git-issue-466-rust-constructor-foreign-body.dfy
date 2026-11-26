module {:extern "Imported"} Base {
  import Peer
  class {:extern "Native"} Native {
    constructor {:extern} () { }
  }
}
module Peer { import Base }
module Refined refines Base { }
