module {:extern "Imported"} Base {
  import Peer
  class {:extern "Native"} Native {
    constructor () { }
  }
}
module Peer { import Base }
module Refined refines Base { }
