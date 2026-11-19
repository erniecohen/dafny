// RUN: %verify --type-system-refresh:false --general-newtypes:false --solver-path "%review-z3" --cores 1 "%s" > "%t"
// RUN: %verify --type-system-refresh --solver-path "%review-z3" --cores 1 "%s" >> "%t"
// RUN: %verify --type-system-refresh:false --general-newtypes:false --additional-axioms --solver-path "%review-z3" --cores 1 "%s" >> "%t"
// RUN: %verify --type-system-refresh --additional-axioms --solver-path "%review-z3" --cores 1 "%s" >> "%t"
// RUN: %verify --type-system-refresh:false --general-newtypes:false --consistent-obligation-checks --solver-path "%review-z3" --cores 1 "%s" >> "%t"
// RUN: %verify --type-system-refresh --consistent-obligation-checks --solver-path "%review-z3" --cores 1 "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// The issue's least-lemma example, including a same-module control.
module Lib {
  least predicate Q(n: nat) { n > 0 && (n == 1 || (n > 1 && Q(n - 2))) }
  least lemma M(n: nat)
    requires Q(n)
    ensures n % 2 == 1
  {
  }
  lemma SameModule(k: ORDINAL, n: nat)
    requires Q#[k](n)
    ensures n % 2 == 1
  {
    M#[k](n);
  }
}
module Client {
  import Lib
  lemma Prefix(k: ORDINAL, n: nat)
    requires Lib.Q#[k](n)
    ensures n % 2 == 1
  {
    Lib.M#[k](n);
  }
  lemma Witness() {
    Prefix(1, 1);
    assert 1 % 2 == 1;
  }
}

module NatLib {
  least predicate Q[nat](n: nat) { n == 0 || Q(n - 1) }
  least lemma M[nat](n: nat)
    requires Q(n)
    ensures n >= 0
  {
  }
}
module NatClient {
  import NatLib
  lemma Prefix(k: nat, n: nat)
    requires NatLib.Q#[k](n)
    ensures n >= 0
  {
    NatLib.M#[k](n);
  }
  lemma Witness() { Prefix(1, 0); }
}

module GreatestLib {
  greatest predicate P(n: nat) { P(n + 1) }
  greatest lemma M(n: nat)
    ensures P(n)
  {
    M(n + 1);
  }
  lemma SameModule(k: ORDINAL, n: nat)
    ensures P#[k](n)
  {
    M#[k](n);
  }
}
module GreatestClient {
  import GreatestLib
  lemma Prefix(k: ORDINAL, n: nat)
    ensures GreatestLib.P#[k](n)
  {
    GreatestLib.M#[k](n);
  }
  lemma Witness() { Prefix(1, 0); }
}
