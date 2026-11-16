// Compressed verification-cache blobs: round trip, earlier uncompressed caches, and corruption.
// RUN: python3 "%repositoryRoot/.github/review/verification-cache-compression.py" "%binaryDir/Dafny.dll" "%review-z3" "%s" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
// UNSUPPORTED: windows

// https://github.com/erniecohen/dafny/issues/388
module M {
  function F(x: int): int { x + 1 }
  lemma Good(x: int) ensures F(x) == x + 1 {}
  lemma Bad(x: int) { assert x == x + 1; }
}
