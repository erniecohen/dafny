// RUN: %verify --standard-libraries:true "%s" > "%t"
// RUN: %exits-with 4 %verify --standard-libraries:true "%S/Inputs/github-issue-354-negative.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Std.Ordinal's Omega, PlusLimit and TimesLimit (https://github.com/erniecohen/dafny/issues/354)
// are opaque ghost functions with proved postconditions, defined from two trusted lemmas,
// Std.Axioms.Ordinals.Infinite and Std.Axioms.Ordinals.Replacement, rather than bodyless
// {:axiom} ghost functions.  This file uses their postconditions and calls the lemmas
// directly.  Inputs/github-issue-354-negative.dfy has the controls: vacuity, a program that
// does not call the lemmas, and a graph that is not a function.

import opened Std.Ordinal
import Std.Axioms.Ordinals

// Every natural number is below Omega.
lemma NatsBelowOmega(n: nat)
  ensures (n as ORDINAL) < Omega()
{
}

// Omega is the least ordinal that is not a natural number.
lemma OmegaIsLeastInfinite(o: ORDINAL)
  requires !o.IsNat
  ensures Omega() <= o
{
  var limit := o - (o.Offset as ORDINAL);
  assert limit.IsLimit && !limit.IsNat && limit <= o;
}

// PlusLimit is above the sums below it, and below every other such bound.
lemma PlusLimitIsLeast(left: ORDINAL, right: ORDINAL, r: ORDINAL, u: ORDINAL)
  requires r < right
  requires forall right' | right' < right :: Plus(left, right') < u
  ensures Plus(left, r) < PlusLimit(left, right) <= u
{
}

lemma TimesLimitIsLeast(left: ORDINAL, right: ORDINAL, r: ORDINAL, u: ORDINAL)
  requires r < right
  requires forall right' | right' < right :: Times(left, right') < u
  ensures Times(left, r) < TimesLimit(left, right) <= u
{
}

// A direct use of Replacement: the ordinals base + n, for every natural number n, are bounded.
ghost predicate BoundsOffsets(base: ORDINAL, u: ORDINAL) {
  forall n: nat :: base + (n as ORDINAL) < u
}

lemma OffsetsAreBounded(base: ORDINAL)
  ensures exists u: ORDINAL :: BoundsOffsets(base, u)
{
  var graph := iset n: nat :: Ordinals.Pair(n as ORDINAL, base + (n as ORDINAL));
  assert Ordinals.Functional(graph) by {
    forall p, q | p in graph && q in graph && p.index == q.index
      ensures p.value == q.value
    {
      var i: nat :| p == Ordinals.Pair(i as ORDINAL, base + (i as ORDINAL));
      var j: nat :| q == Ordinals.Pair(j as ORDINAL, base + (j as ORDINAL));
    }
  }
  Ordinals.Replacement(graph, Omega());
  var u: ORDINAL :| Ordinals.StrictUpperBound(graph, Omega(), u);
  forall n: nat
    ensures base + (n as ORDINAL) < u
  {
    NatsBelowOmega(n);
    assert Ordinals.Pair(n as ORDINAL, base + (n as ORDINAL)) in graph;
  }
  assert BoundsOffsets(base, u);
}
