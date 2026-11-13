// Opaque functions get a generated reveal lemma, which states their definition and is not a premise.
module Opaque {
  opaque function F(): int { 1 }

  function {:opaque} G(): int { 2 }

  lemma L()
    ensures F() + G() == 3
  {
    reveal F(), G();
  }
}
