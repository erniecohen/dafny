module Library {
  export reveals T provides T.Final
  trait {:termination false} T {
    function {:termination_closed} Final(): nat { 0 }
  }
}
module Client {
  import Library
  class C extends Library.T {
    function Final(): nat { 1 }
  }
}
