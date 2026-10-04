// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
module Definitions {
  opaque function F(): int { 7 }
}
module Client {
  import opened D = Definitions
  lemma UseImported() {
    reveal D.F();
    assert D.F() == 7;
    hide D.F;
    assert D.F() == 7;
    assert false;
  }
}
