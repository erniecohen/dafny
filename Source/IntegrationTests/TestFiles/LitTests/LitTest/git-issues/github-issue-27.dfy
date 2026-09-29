// RUN: %baredafny resolve --type-system-refresh:false "%s" > "%t"
// RUN: %baredafny resolve --type-system-refresh:true "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

module Library {
  least predicate Reach(n: nat) { n == 0 || Reach(n - 1) }
  greatest predicate Inf(n: nat) { Inf(n + 1) }
  least lemma ReachLemma(n: nat) ensures Reach(n)
  greatest lemma InfLemma(n: nat) ensures Inf(n)
  predicate Ordinary(n: nat) { n == 0 }
}

abstract module Client {
  import L : Library
  predicate Uses(n: nat) { L.Reach(n) && L.Inf(n) && L.Ordinary(n) }
  predicate Prefix(n: nat) { L.Reach#[1](n) && L.Inf#[1](n) }
  lemma Calls(n: nat) {
    L.ReachLemma(n);
    L.InfLemma(n);
    L.ReachLemma#[1](n);
    L.InfLemma#[1](n);
  }
}

abstract module OpenedClient {
  import opened L : Library
  predicate Uses(n: nat) { Reach(n) && Inf(n) }
}

abstract module AbstractLibrary {
  least predicate Reach(n: nat)
  greatest predicate Inf(n: nat)
  least lemma ReachLemma(n: nat) ensures Reach(n)
  greatest lemma InfLemma(n: nat) ensures Inf(n)
}

abstract module AbstractClient {
  import L : AbstractLibrary
}

module ClassLibrary {
  class C {
    least predicate Reach(n: nat) { n == 0 || Reach(n - 1) }
    greatest predicate Inf(n: nat) { Inf(n + 1) }
  }
}

abstract module ClassClient {
  import L : ClassLibrary
}

// Ordinary imports and abstract imports without extreme declarations still work.
module ConcreteClient {
  import L = Library
}
module OrdinaryLibrary {
  predicate P() { true }
  lemma Lemma() ensures P() {}
}
abstract module OrdinaryClient {
  import L : OrdinaryLibrary
}
