module {:extern "Imported"} Base {
  import Peer
  class {:extern "Native"} Native {
    constructor () { value := 0; }
    var value: int
  }
}
module Peer { import Base }
module Refined refines Base { }
