// RUN: %exits-with 4 %verify --type-system-refresh --general-newtypes --general-traits=full --consistent-obligation-checks --solver-path "%review-z3" --cores 1 "%s" > "%t"
// RUN: %exits-with 4 %verify --type-system-refresh --general-newtypes --general-traits=full --consistent-obligation-checks --additional-axioms --solver-path "%review-z3" --cores 1 "%s" >> "%t"
// RUN: %exits-with 4 %verify --type-system-refresh --general-newtypes --general-traits=full --solver-path "%review-z3" --cores 1 "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

include "git-issue-516.dfy"

module Controls {
  import Original
  import Constants
  import RealBase
  import BitvectorBase

  // These are reachable: the positive regression constructs the same values.
  lemma FalseAfterRoundTrip() {
    var n := 0 as Original.N;
    var g := n as Original.G;
    var m := g as Original.N;
    assert false; // error
  }
  lemma FalseAfterConstants() {
    var n := 1 as Constants.N;
    var g := n as Constants.G;
    var m := g as Constants.N;
    assert false; // error
  }
  lemma UncheckedDowncast(g: Original.G) {
    var m := g as Original.N; // error
  }
  lemma WrongConstraint() {
    var n := 10 as Original.N; // error
  }
  lemma UncheckedRealDowncast(g: RealBase.G) {
    var m := g as RealBase.N; // error
  }
  lemma UncheckedBitvectorDowncast(g: BitvectorBase.G) {
    var m := g as BitvectorBase.N; // error
  }
}

