// Built with --default-function-opacity, every function gets a generated reveal lemma.
module Opaque {
  function F(): int { 1 }

  lemma L()
    ensures F() == 1
  {
    reveal F();
  }
}
