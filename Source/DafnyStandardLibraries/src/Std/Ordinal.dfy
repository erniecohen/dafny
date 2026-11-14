/*******************************************************************************
 *  Copyright by the contributors to the Dafny Project
 *  SPDX-License-Identifier: MIT
 *******************************************************************************/

module Std.Ordinal {

  // Omega, PlusLimit and TimesLimit are provided without their bodies, so an importer sees only
  // their postconditions, as it did when they were bodyless axioms, and its proofs carry none of
  // the definitions below.  The helpers that prove them are not exported.
  export
    reveals Plus, Times, Max
    provides Omega, PlusLimit, TimesLimit, Succ, PlusDefinition, PlusStrictlyIncreasingOnRight,
             PlusIncreasingOnLeft, SuccStrictlyIncreasing, SuccIncreasing, PlusIsAssociative,
             TimesLeftIdentity, TimesRightIdentity, TimesStrictlyIncreasingOnRight, TimesIncreasingOnLeft,
             TimesDistributesOnLeft, RadixStrictlyIncreasing

  import Ordinals = Axioms.Ordinals

  /**
   The first infinite ordinal, `ω`: the least ordinal that is not a natural
   number.
   */
  ghost function Omega(): ORDINAL
    ensures !Omega().IsNat
    ensures Omega().IsLimit
    ensures forall other: ORDINAL | other.IsLimit && other != 0 :: Omega() <= other
  {
    Ordinals.Infinite();
    var o: ORDINAL :| !o.IsNat;
    LeastInfinite(o);
    var w: ORDINAL :| !w.IsNat && forall m: ORDINAL | m < w :: m.IsNat;
    assert w.IsLimit by {
      if !w.IsLimit {
        assert !(w - 1).IsNat && w - 1 < w;
      }
    }
    w
  }

  /** There is a least ordinal that is not a natural number. */
  lemma LeastInfinite(o: ORDINAL)
    requires !o.IsNat
    ensures exists w: ORDINAL :: !w.IsNat && forall m: ORDINAL | m < w :: m.IsNat
    decreases o
  {
    if o': ORDINAL :| o' < o && !o'.IsNat {
      LeastInfinite(o');
    }
  }

  /**
   The least ordinal above every value of `graph` at an index below `bound`.
   */
  ghost function LeastStrictUpperBound(graph: iset<Ordinals.Pair>, bound: ORDINAL): (result: ORDINAL)
    requires Ordinals.Functional(graph)
    ensures Ordinals.StrictUpperBound(graph, bound, result)
    ensures forall u: ORDINAL {:trigger Ordinals.StrictUpperBound(graph, bound, u)} |
              Ordinals.StrictUpperBound(graph, bound, u) :: result <= u
  {
    Ordinals.Replacement(graph, bound);
    var u: ORDINAL :| Ordinals.StrictUpperBound(graph, bound, u);
    LeastStrictUpperBoundExists(graph, bound, u);
    var result: ORDINAL :|
      && Ordinals.StrictUpperBound(graph, bound, result)
      && forall u': ORDINAL {:trigger Ordinals.StrictUpperBound(graph, bound, u')} |
           Ordinals.StrictUpperBound(graph, bound, u') :: result <= u';
    result
  }

  /** A strict upper bound of `graph` below `bound` is at least the least one. */
  lemma LeastStrictUpperBoundExists(graph: iset<Ordinals.Pair>, bound: ORDINAL, u: ORDINAL)
    requires Ordinals.StrictUpperBound(graph, bound, u)
    ensures exists m: ORDINAL {:trigger Ordinals.StrictUpperBound(graph, bound, m)} ::
              && Ordinals.StrictUpperBound(graph, bound, m)
              && forall m': ORDINAL {:trigger Ordinals.StrictUpperBound(graph, bound, m')} |
                   Ordinals.StrictUpperBound(graph, bound, m') :: m <= m'
    decreases u
  {
    if u': ORDINAL :| u' < u && Ordinals.StrictUpperBound(graph, bound, u') {
      LeastStrictUpperBoundExists(graph, bound, u');
    }
  }

  // Additional axioms about addition

  lemma {:axiom} Succ(a: ORDINAL, b: ORDINAL)
    ensures a < b <==> a + 1 <= b

  /**
   The sum of `left` and a limit `right`: the least ordinal above
   `Plus(left, right')` for every `right'` below `right`.
   */
  ghost function PlusLimit(left: ORDINAL, right: ORDINAL): (result: ORDINAL)
    ensures forall right' | right' < right :: Plus(left, right') < result
    ensures forall result' {:trigger} |
              forall right' | right' < right :: Plus(left, right') < result' :: result <= result'
    decreases right, 0
  {
    var graph := iset right': ORDINAL | right' < right :: Ordinals.Pair(right', Plus(left, right'));
    assert Ordinals.Functional(graph) by {
      forall p, q | p in graph && q in graph && p.index == q.index
        ensures p.value == q.value
      {
        var i: ORDINAL :| i < right && p == Ordinals.Pair(i, Plus(left, i));
        var j: ORDINAL :| j < right && q == Ordinals.Pair(j, Plus(left, j));
      }
    }
    var result := LeastStrictUpperBound(graph, right);
    forall right' | right' < right
      ensures Plus(left, right') < result
    {
      assert Ordinals.Pair(right', Plus(left, right')) in graph;
    }
    forall result' {:trigger} | forall right' | right' < right :: Plus(left, right') < result'
      ensures result <= result'
    {
      forall p | p in graph && p.index < right
        ensures p.value < result'
      {
        var i: ORDINAL :| i < right && p == Ordinals.Pair(i, Plus(left, i));
      }
      assert Ordinals.StrictUpperBound(graph, right, result');
    }
    result
  }

  ghost function Plus(left: ORDINAL, right: ORDINAL): ORDINAL
    decreases right
  {
    if right == 0 then
      0
    else if right.IsLimit then
      PlusLimit(left, right)
    else
      Plus(left, right - 1) + 1
  }

  lemma {:axiom} PlusDefinition(left: ORDINAL, right: ORDINAL)
    ensures Plus(left, right) == left + right

  lemma {:axiom} PlusStrictlyIncreasingOnRight(left: ORDINAL, right: ORDINAL, right': ORDINAL)
    requires right < right'
    ensures left + right < left + right'
    decreases right

  lemma {:axiom} PlusIncreasingOnLeft(left: ORDINAL, left': ORDINAL, right: ORDINAL)
    requires left <= left'
    ensures left + right <= left' + right

  lemma SuccStrictlyIncreasing(a: ORDINAL, b: ORDINAL)
    requires a < b
    ensures a + 1 < b + 1
  {
    PlusDefinition(a, 1);
    PlusDefinition(b, 1);
  }

  lemma SuccIncreasing(a: ORDINAL, b: ORDINAL)
    requires a <= b
    ensures a + 1 <= b + 1
  {
    PlusDefinition(a, 1);
    PlusDefinition(b, 1);
  }

  lemma {:axiom} PlusIsAssociative(x: ORDINAL, y: ORDINAL, z: ORDINAL)
    decreases z
    ensures (x + y) + z == x + (y + z)

  // Multiplication and axioms about multiplication

  /**
   The product of `left` and a limit `right`: the least ordinal above
   `Times(left, right')` for every `right'` below `right`.
   */
  ghost function TimesLimit(left: ORDINAL, right: ORDINAL): (result: ORDINAL)
    ensures forall right' | right' < right :: Times(left, right') < result
    ensures forall result' {:trigger} | (forall right' | right' < right :: Times(left, right') < result') :: result <= result'
    decreases right, 0
  {
    var graph := iset right': ORDINAL | right' < right :: Ordinals.Pair(right', Times(left, right'));
    assert Ordinals.Functional(graph) by {
      forall p, q | p in graph && q in graph && p.index == q.index
        ensures p.value == q.value
      {
        var i: ORDINAL :| i < right && p == Ordinals.Pair(i, Times(left, i));
        var j: ORDINAL :| j < right && q == Ordinals.Pair(j, Times(left, j));
      }
    }
    var result := LeastStrictUpperBound(graph, right);
    forall right' | right' < right
      ensures Times(left, right') < result
    {
      assert Ordinals.Pair(right', Times(left, right')) in graph;
    }
    forall result' {:trigger} | forall right' | right' < right :: Times(left, right') < result'
      ensures result <= result'
    {
      forall p | p in graph && p.index < right
        ensures p.value < result'
      {
        var i: ORDINAL :| i < right && p == Ordinals.Pair(i, Times(left, i));
      }
      assert Ordinals.StrictUpperBound(graph, right, result');
    }
    result
  }

  ghost function Times(left: ORDINAL, right: ORDINAL): (result: ORDINAL)
    decreases right
  {
    if right == 0 then
      0
    else if right.IsLimit then
      TimesLimit(left, right)
    else
      Times(left, right - 1) + left
  }

  lemma TimesLeftIdentity(o: ORDINAL)
    ensures Times(1, o) == o
  {
    if o.IsLimit {
      assert Times(1, o) <= o;
    } else {
      TimesLeftIdentity(o - 1);
    }
  }

  lemma TimesRightIdentity(o: ORDINAL)
    ensures Times(o, 1) == o
  {}

  lemma {:axiom} TimesStrictlyIncreasingOnRight(left: ORDINAL, right: ORDINAL, right': ORDINAL)
    requires 0 < left
    requires right < right'
    ensures Times(left, right) < Times(left, right')

  lemma {:axiom} TimesIncreasingOnLeft(left: ORDINAL, left': ORDINAL, right: ORDINAL)
    requires left <= left'
    ensures Times(left, right) < Times(left', right)

  lemma {:axiom} TimesDistributesOnLeft(left: ORDINAL, right: ORDINAL, right': ORDINAL)
    ensures Times(left, right + right') == Times(left, right) + Times(left, right')
    decreases right'

  // Helpful lemmas and utilities

  /** Maximum of two ordinals  */
  function Max(a: ORDINAL, b: ORDINAL): ORDINAL
  {
    if a < b
    then b
    else a
  }

  lemma RadixStrictlyIncreasing(base: ORDINAL, a: ORDINAL, a': ORDINAL, b: ORDINAL)
    requires a < a'
    requires b < base
    ensures Times(base, a) + b < Times(base, a')
  {
    Succ(a, a');
    assert a + 1 <= a';

    TimesDistributesOnLeft(base, a', 1);
    assert Times(base, a' + 1) == Times(base, a') + Times(base, 1);
    TimesLeftIdentity(base);
    assert Times(base, a' + 1) == Times(base, a') + base;

    if a + 1 == a' {
      calc {
        Times(base, a) + b;
      < { PlusStrictlyIncreasingOnRight(Times(base, a), b, base); }
        Times(base, a) + base;
        Times(base, a + 1);
        Times(base, a');
      }
    } else {
      calc {
        Times(base, a) + b;
      < { PlusStrictlyIncreasingOnRight(Times(base, a), b, base); }
        Times(base, a) + base;
        Times(base, a + 1);
      <= { TimesStrictlyIncreasingOnRight(base, a + 1, a'); }
        Times(base, a');
      }
    }
  }
}