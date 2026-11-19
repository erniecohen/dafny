// RUN: %exits-with 4 %verify --type-system-refresh:false --general-newtypes:false --solver-path "%review-z3" --cores 1 "%s" > "%t"
// RUN: %exits-with 4 %verify --type-system-refresh --solver-path "%review-z3" --cores 1 "%s" >> "%t"
// RUN: %exits-with 4 %verify --type-system-refresh:false --general-newtypes:false --additional-axioms --solver-path "%review-z3" --cores 1 "%s" >> "%t"
// RUN: %exits-with 4 %verify --type-system-refresh --additional-axioms --solver-path "%review-z3" --cores 1 "%s" >> "%t"
// RUN: %exits-with 4 %verify --type-system-refresh:false --general-newtypes:false --consistent-obligation-checks --solver-path "%review-z3" --cores 1 "%s" >> "%t"
// RUN: %exits-with 4 %verify --type-system-refresh --consistent-obligation-checks --solver-path "%review-z3" --cores 1 "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

include "git-issue-471.dfy"

module Controls {
  import Lib
  import NatLib
  import GreatestLib

  // Each premise is inhabited: the positive file calls the same prefix lemmas.
  lemma LeastFalse() {
    Lib.M#[1](1);
    assert false;
  }
  lemma NatLeastFalse() {
    NatLib.M#[1](0);
    assert false;
  }
  lemma GreatestFalse() {
    GreatestLib.M#[1](0);
    assert false;
  }

  // A missing declaration must be fixed without discarding call preconditions.
  lemma LeastPrecondition() {
    Lib.M#[0](0);
  }
  lemma NatLeastPrecondition() {
    NatLib.M#[0](0);
  }
}
