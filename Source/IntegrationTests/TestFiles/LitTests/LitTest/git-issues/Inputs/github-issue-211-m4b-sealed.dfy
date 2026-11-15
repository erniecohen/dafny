module Library {
  trait Sealed { method F() }
}
module Client {
  import Library
  @AssumeCrossModuleTermination
  class C extends Library.Sealed { method F() { } }
}
