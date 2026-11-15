module Library {
  trait {:termination false} Open { }
  trait Sealed extends Open { }
}
module Client {
  import Alias = Library
  @AssumeCrossModuleTermination
  class C extends Alias.Sealed { }
}
