// Fresh processes and a shared on-disk cache require the external CLI rather than the singleton verdict planner.
// RUN: python3 "%S/Inputs/github-issue-241-release-persistent.py" "%binaryDir/Dafny.dll" "%review-z3" "%s" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
// UNSUPPORTED: windows

// https://github.com/erniecohen/dafny/issues/241: --release-verified-modules with
// --persistent-translation (https://github.com/erniecohen/dafny/issues/261) and
// --translation-workers.  The window of two admits the Late modules only after earlier modules
// were verified and released, so their persistent lookups run while released bodies exist.
module Library {
  function Double(n: nat): nat { 2 * n }
  lemma DoubleEven(n: nat) ensures Double(n) % 2 == 0 {
    var m := n;
    assert Double(m) == m + m;
  }
}

module Impl {
  import L = Library
  method Choose(n: nat) returns (r: nat) ensures r % 2 == 0 {
    var k := L.Double(n);
    L.DoubleEven(n);
    r := k;
  }
}

module Late1 {
  import L = Library
  lemma Even1(n: nat) ensures L.Double(n + 1) % 2 == 0 { L.DoubleEven(n + 1); }
}

module Late2 {
  import I = Impl
  method Use(n: nat) returns (r: nat) ensures r % 2 == 0 {
    var x := I.Choose(n);
    r := x;
  }
}

module Late3 {
  import L = Library
  method Sum(n: nat) returns (s: nat) ensures s == L.Double(n) {
    var a := n;
    s := a + a;
  }
}

module Late4 {
  import L = Library
  lemma Even4(n: nat) ensures L.Double(n) % 2 == 0 { L.DoubleEven(n); }
}
