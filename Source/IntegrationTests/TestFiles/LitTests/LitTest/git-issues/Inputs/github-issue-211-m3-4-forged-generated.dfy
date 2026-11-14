// A function that claims to be generated.  The serialization dropped it from the stored program, where
// Bad's postcondition then named G.f, which is 2 (https://github.com/erniecohen/dafny/issues/325).
module G {
  function f(): int { 2 }
}

module Bad {
  import opened G

  function {:auto_generated} f(): int { 1 }

  lemma Bad() ensures f() == 1 {}
}
