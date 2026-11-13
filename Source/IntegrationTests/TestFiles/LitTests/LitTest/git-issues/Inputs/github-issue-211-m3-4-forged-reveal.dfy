// A lemma that claims to be Dafny's generated reveal lemma.  The serialization dropped it from the stored
// program, where the call in Bad then resolved to G.L (https://github.com/erniecohen/dafny/issues/325).
module G {
  lemma L() ensures true {}
}

module Bad {
  import opened G

  lemma {:axiom} {:auto_generated} {:opaque_reveal} L() ensures false

  lemma Bad() ensures 1 == 2 {
    L();
  }
}
