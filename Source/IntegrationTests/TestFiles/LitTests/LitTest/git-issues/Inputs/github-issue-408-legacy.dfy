// The issue's program (https://github.com/erniecohen/dafny/issues/408), run under the hidden
// --legacy-unchecked-import-binding (RUN line in ../github-issue-408.dfy): it verifies, and so
// proves false.

abstract module A {
  function F(): int
  lemma L() ensures F() > 0
}

module Other {
  function F(): int { 0 }
  lemma L() { }
}

abstract module B {
  import X : A
  lemma P() ensures X.F() > 0 { X.L(); }
}

module C refines B {
  import X = Other
  lemma Bad() ensures false { P(); }
}
