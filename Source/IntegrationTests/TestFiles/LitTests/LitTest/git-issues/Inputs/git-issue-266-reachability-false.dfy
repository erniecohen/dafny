include "../git-issue-266-reachability.dfy"

// Keep each accepted construction and application reachable before the false control.
module FalseControls {
  import DatatypeArgument
  import ConstantArgument
  import DatatypeStorage
  import ConstantStorage
  method FalseDatatypeArgument() {
    DatatypeArgument.Positive();
    assert false;
  }
  method FalseConstantArgument() {
    ConstantArgument.Positive();
    assert false;
  }
  method FalseDatatypeStorage() {
    DatatypeStorage.Positive();
    assert false;
  }
  method FalseConstantStorage() {
    ConstantStorage.Positive();
    assert false;
  }
}
