module {:extern "Imported"} Base {
  import Peer
  class {:extern "Native"} Native {
    constructor {:extern "Elsewhere", "Init"} () { }
  }
}
module Peer { import Base }
module Refined refines Base { }
