// The program of https://github.com/erniecohen/dafny/issues/410 under the hidden
// --legacy-inherited-name-capture: B's F captures the F of the inherited lemma P, and B proves false.
module L {
  function F(): int { 1 }
}
module A {
  import opened L
  lemma P() ensures F() == 1 { }
}
module B refines A {
  function F(): int { 2 }
  lemma Bad() ensures false { P(); }
}
