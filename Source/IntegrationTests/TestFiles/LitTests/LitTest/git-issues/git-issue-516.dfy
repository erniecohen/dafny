// RUN: %verify --type-system-refresh --general-newtypes --general-traits=full --consistent-obligation-checks --solver-path "%review-z3" --cores 1 "%s" > "%t"
// RUN: %verify --type-system-refresh --general-newtypes --general-traits=full --consistent-obligation-checks --additional-axioms --solver-path "%review-z3" --cores 1 "%s" >> "%t"
// RUN: %verify --type-system-refresh --general-newtypes --general-traits=full --solver-path "%review-z3" --cores 1 "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// The reported crash: the downcast checks x < 10 on a boxed trait receiver.
module Original {
  trait G {
    const k: int := 1
    function F(): int { k }
  }
  newtype N extends G = x: int | x < 10
  method M(n: N) {
    var g := n as G;
    var m := g as N;
  }
  lemma Witness() {
    var n := 0 as N;
    var g := n as G;
    var m := g as N;
    assert m == n;
    assert g.k == 1;
    assert g.F() == 1;
  }
}

// Constant result representations do not determine the receiver's representation.
module Constants {
  trait G {
    const b: bool := true
    const r: real := 1.5
    const v: bv8 := 3
    const c: char := 'a'
    const s: seq<int> := [1, 2]
    function F(): bool { b && r == 1.5 && v == 3 && c == 'a' && s == [1, 2] }
  }
  newtype N extends G = x: int | 0 <= x < 10
  lemma RoundTrip() {
    var n := 1 as N;
    var g: G := n;
    var m := g as N;
    assert m == n;
    assert g.F();
    assert g is N;
  }
  method Upcast(n: N) {
    var g := n as G;
    assert g is N;
    assert g.F();
  }
}

module RealBase {
  trait G { const k: bool := true }
  newtype N extends G = x: real | 0.0 <= x < 10.0
  lemma RoundTrip() {
    var n := 1.0 as N;
    var g := n as G;
    var m := g as N;
    assert m == n;
    assert g.k;
  }
}

module BitvectorBase {
  trait G { const k: seq<int> := [1] }
  newtype N extends G = x: bv8 | x < 10
  lemma RoundTrip() {
    var n := 1 as N;
    var g := n as G;
    var m := g as N;
    assert m == n;
    assert g.k == [1];
  }
}

// The legacy resolver rejects these general-newtype conversions before verification.
// RUN: %exits-with 2 %verify --type-system-refresh:false --general-newtypes:false --general-traits=full --solver-path "%review-z3" --cores 1 "%s" > "%t.legacy"
// RUN: %exits-with 2 %verify --type-system-refresh:false --general-newtypes:false --general-traits=full --consistent-obligation-checks --solver-path "%review-z3" --cores 1 "%s" >> "%t.legacy"
// RUN: %diff "%s.legacy.expect" "%t.legacy"
