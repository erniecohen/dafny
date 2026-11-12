module Library {
  export provides T
  trait T {}
}
module Client {
  import Library
  method Bounded(t: Library.T) decreases {:global} 0 {}
}
