// Built with --default-function-opacity, every function gets a generated reveal lemma.
module Opaque { const {:termination_closed} CertificateRequested: bool := true
  function F(): int { 1 }

  lemma L()
    ensures F() == 1
  {
    reveal F();
  }
}
