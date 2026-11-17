// Extreme predicates and lemmas that --modular-termination accepts.  An extreme predicate cannot declare a
// decreases clause, so it has no bound: a caller without one may call it, or dispatch to an extreme predicate
// slot, as Top does below.  An extreme lemma's bound is its prefix lemma's, so a co-call
// in its body runs under it.  A bounded caller may not evaluate an extreme predicate that reaches a dispatch,
// such as G below, since that predicate has no bound: it is TOP.
module Tr {
  trait T {
    ghost function A(other: T): int decreases {:global} 0
    least predicate C(other: T)
    lemma N(other: T) decreases {:global} 0
  }
  class X extends T {
    constructor () { }
    ghost function A(other: T): int decreases {:global} 0 { 0 }
    least predicate C(other: T) { true }
    lemma N(other: T) decreases {:global} 0 { }
  }
  least predicate P(t: T, o: T) { t.A(o) == 0 }
  greatest predicate G(t: T, o: T) { t.A(o) == 0 && G(t, o) }
  least lemma L(t: T, o: T, x: nat) requires x == 0 || L'(x) decreases {:global} 1 { t.N(o); }
  least predicate L'(x: nat) { x == 0 || L'(x - 1) }
  greatest predicate H(x: int) { H(x) }
  greatest lemma GL(t: T, o: T, x: int) ensures H(x) decreases {:global} 1 { t.N(o); GL(t, o, x); }
}
module Y { import Tr
  class Y extends Tr.T {
    constructor () { }
    ghost function A(other: Tr.T): int decreases {:global} 0 { 1 }
    least predicate C(other: Tr.T) { false }
    lemma N(other: Tr.T) decreases {:global} 0 { }
  }
  ghost function Top(t: Tr.T, o: Tr.T): bool { Tr.P(t, o) && Tr.G(t, o) && t.C(o) }
  lemma Bounded(t: Tr.T, o: Tr.T) decreases {:global} 2 { Tr.L(t, o, 0); Tr.GL(t, o, 0); }
}
