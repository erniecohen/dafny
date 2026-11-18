// A library that binds the results of lemmas, for github-issue-199-library.dfy.

module Lib {
  export
    reveals Above
    provides Find, AboveIsAbove, ByPrivate
  export Provided
    provides Above, Find

  lemma Find(n: nat) returns (m: nat)
    ensures m > n
  {
    m := n + 1;
  }

  ghost function Above(n: nat): nat {
    var m := Find(n); m
  }

  lemma AboveIsAbove(n: nat)
    ensures Above(n) > n
  {
  }

  lemma Private(n: nat) returns (m: nat)
    ensures m > n
  {
    m := n + 1;
  }

  ghost function ByPrivate(n: nat): nat {
    var m := Private(n); m
  }
}

abstract module Abstract {
  lemma Find(n: nat) returns (m: nat)
    ensures m > n

  ghost function Above(n: nat): nat {
    var m := Find(n); m
  }

  lemma AboveIsAbove(n: nat)
    ensures Above(n) > n
  {
  }
}

module Concrete refines Abstract {
  lemma Find(n: nat) returns (m: nat)
    ensures m == n + 1
  {
    m := n + 1;
  }

  lemma AboveIsNext(n: nat)
    ensures Above(n) == n + 1
  {
  }
}
