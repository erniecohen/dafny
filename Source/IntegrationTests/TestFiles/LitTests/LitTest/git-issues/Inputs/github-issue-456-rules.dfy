// https://github.com/erniecohen/dafny/issues/456: the rules for least and greatest lemmas and predicates and for co-recursive calls.
// Every member Ann breaks them in one way and must be rejected.
module Lib {
  trait {:termination false} T {
    lemma L(n: nat)
      ensures false
      decreases {:global} n
    ghost predicate P(n: nat)
      decreases {:global} n
    function G(n: nat): nat
      decreases {:global} n
  }
}
module Client {
  import opened Lib
  least predicate Q(n: nat) { true }
  greatest predicate GQ(n: nat) { true }
  least predicate LP(t: T, n: nat) { t.P(n) }
  greatest predicate GP(t: T, n: nat) { t.P(n) && GP(t, n + 1) }
  ghost function Help(t: T, n: nat): bool { LP(t, n) }

  // the recursive call of a least lemma raises the bound
  least lemma A01(t: T, n: nat) requires Q(n) ensures false decreases {:global} n
  { if * { A01(t, n + 1); } else if n > 0 { t.L(n - 1); } else { assume false; } }
  // a least lemma with no bound that dispatches, called from code under a bound
  least lemma NoBound(t: T, n: nat) requires Q(n) ensures false { t.L(n); }
  lemma A02(t: T, n: nat) requires Q(n) ensures false decreases {:global} n { NoBound(t, n); }
  // greatest lemma: dispatch at the caller's own bound
  greatest lemma A03(t: T, n: nat) ensures GQ(n) decreases {:global} n { t.L(n); }
  // greatest lemma: the recursive call raises the bound
  greatest lemma A04(t: T, n: nat) ensures GQ(n) decreases {:global} n { if n > 0 { t.L(n - 1); } A04(t, n + 1); }
  // a prefix lemma called with "#[k]", raising the bound
  least lemma Bounded(t: T, n: nat) requires Q(n) ensures false decreases {:global} n { if n > 0 { t.L(n - 1); } else { assume false; } }
  lemma A05(t: T, n: nat, k: ORDINAL) requires Q#[k](n + 1) ensures false decreases {:global} n { Bounded#[k](t, n + 1); }
  // a least predicate reached through a function with no bound
  ghost predicate A06(t: T, n: nat) decreases {:global} n { Help(t, n) }
  // a greatest predicate, and its prefix predicate, called from code under a bound
  ghost predicate A07(t: T, n: nat) decreases {:global} n { GP(t, n) }
  ghost predicate A08(t: T, n: nat, k: ORDINAL) decreases {:global} n { GP#[k](t, n) }
  // a least predicate in a precondition, a postcondition and a subset-type constraint of code under a bound
  lemma A09(t: T, n: nat) requires LP(t, n) decreases {:global} n { }
  lemma A10(t: T, n: nat) ensures LP(t, n) || true decreases {:global} n { }
  // a member least lemma with a bound that mentions "this"
  class Cell {
    const c: nat
    const t: T
    constructor(c: nat, t: T) { this.c := c; this.t := t; }
    least lemma A11(n: nat) requires Q(n) ensures false decreases {:global} c + n { t.L(c + n); }
    least lemma A12(n: nat) requires Q(n) ensures false decreases {:global} c + n { if * { A12(n + 1); } else { t.L(c + n); assume false; } }
  }
  // a generic least lemma
  least lemma A13<X>(t: T, s: seq<X>) requires Q(|s|) ensures false decreases {:global} |s| { t.L(|s|); }
  // co-recursion: mutual, raising the bound on one of the two calls
  codatatype Stream = Cons(head: nat, tail: Stream)
  function A15(n: nat, t: T): Stream decreases {:global} n { Cons(if n > 0 then t.G(n - 1) else 0, Other(n, t)) }
  function Other(n: nat, t: T): Stream decreases {:global} n + 1 { Cons(t.G(n), A15(n, t)) }
  // co-recursion: the function has no bound, and is called from code under a bound
  function Unbounded(n: nat, t: T): Stream { Cons(t.G(n), Unbounded(n + 1, t)) }
  function A16(n: nat, t: T): nat decreases {:global} n { Unbounded(n, t).head }
}
