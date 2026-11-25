include "../git-issue-266-persistence.dfy"

module FalseControls {
  import Persistence

  method FalseAfterStorageAndReturn() {
    Persistence.Positive();
    assert false;
  }
}
