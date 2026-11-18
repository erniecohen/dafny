// Translating a cast through a deep chain of newtypes took time cubic in the chain's depth
// (https://github.com/erniecohen/dafny/issues/378).  The translator now remembers the ancestor of each
// type it converts.  The verdicts and errors are unchanged: the round trips verify, and a cast
// into a constrained newtype still fails where the value may be out of range.
// RUN: %exits-with 4 %verify --show-snippets:false "%s" > "%t"
// RUN: %exits-with 4 %verify --show-snippets:false --extended-newtype-bases "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

module Deep {
  type Pair = int
  newtype L0 = Pair
  newtype L1 = L0
  newtype L2 = L1
  newtype L3 = L2
  newtype L4 = L3
  newtype L5 = L4
  newtype L6 = L5
  newtype L7 = L6
  newtype L8 = L7
  newtype L9 = L8
  newtype L10 = L9
  newtype L11 = L10
  newtype L12 = L11
  newtype L13 = L12
  newtype L14 = L13
  newtype L15 = L14
  newtype L16 = L15
  newtype L17 = L16
  newtype L18 = L17
  newtype L19 = L18
  newtype L20 = L19
  newtype L21 = L20
  newtype L22 = L21
  newtype L23 = L22
  newtype L24 = L23
  newtype L25 = L24
  newtype L26 = L25
  newtype L27 = L26
  newtype L28 = L27
  newtype L29 = L28
  newtype L30 = L29
  newtype L31 = L30
  newtype L32 = L31
  newtype L33 = L32
  newtype L34 = L33
  newtype L35 = L34
  newtype L36 = L35
  newtype L37 = L36
  newtype L38 = L37
  newtype L39 = L38
  newtype L40 = L39

  lemma RoundTrip(b: Pair)
    ensures (((((((((((((((((((((((((((((((((((((((((b as L0) as L1) as L2) as L3) as L4) as L5) as L6) as L7) as L8) as L9) as L10) as L11) as L12) as L13) as L14) as L15) as L16) as L17) as L18) as L19) as L20) as L21) as L22) as L23) as L24) as L25) as L26) as L27) as L28) as L29) as L30) as L31) as L32) as L33) as L34) as L35) as L36) as L37) as L38) as L39) as L40) as Pair == b
  {}

  lemma ToReal(b: L40)
    ensures (b as real) as int == b as int
  {}

  newtype Small = x: L40 | 0 <= x < 10

  method Fits(b: L40) returns (r: bv8)
    requires 0 <= b < 10
    ensures r as int == b as int
  {
    r := ((b as Small) as L20) as bv8;
  }

  method MayNotFit(b: L40) returns (s: Small) {
    s := b as Small; // error: b may be out of range
  }
}

module Provider {
  export reveals N, M
  newtype N = int
  newtype M = N
}

module Client {
  import Provider

  lemma RoundTrip(n: Provider.M)
    ensures ((n as Provider.N) as int) as Provider.M == n
  {}
}
