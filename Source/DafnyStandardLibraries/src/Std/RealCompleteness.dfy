/*******************************************************************************
 *  Copyright by the contributors to the Dafny Project
 *  SPDX-License-Identifier: MIT
 *******************************************************************************/

/**
 Completeness of `real`: least upper bounds of sets of reals.

 Dafny's `real` is axiomatized as an ordered field and has no completeness
 facts.  This module supplies them through one trusted lemma, `Complete`, which
 is used only by an explicit call: a lemma without a body is a procedure, not a
 global axiom, so a program that does not call it is unaffected.  Everything
 else here is verified from `Complete`.
 */
module Std.RealCompleteness {

  /** `b` is an upper bound of `s`. */
  ghost predicate upper(s: iset<real>, b: real) {
    forall x {:trigger x in s} :: x in s ==> x <= b
  }

  /**
   The trusted fact, and the only axiom of this module: every nonempty set of
   reals that is bounded above has a least upper bound.  This is true of the
   real numbers ℝ, which are Dafny's model of `real`.

   `Complete` has no preconditions.  Its conclusions hold only when `w` is a
   member of `s` and `b` is an upper bound of `s`; for any other arguments
   (an empty set, an unbounded set, a bad witness) its result is an unspecified
   real, about which nothing is known.  Use `ExistsLub` or `Sup` for the
   checked forms.
   */
  lemma {:axiom} Complete(s: iset<real>, w: real, b: real) returns (u: real)
    ensures w in s && upper(s, b) ==> upper(s, u)
    ensures w in s && upper(s, b) ==> forall v {:trigger upper(s, v)} :: upper(s, v) ==> u <= v

  /** `s` is nonempty and bounded above. */
  ghost predicate Eligible(s: iset<real>) {
    (exists w :: w in s) && (exists b :: upper(s, b))
  }

  /** `u` is the least upper bound of `s`. */
  ghost predicate IsLub(s: iset<real>, u: real) {
    upper(s, u) && forall v {:trigger upper(s, v)} :: upper(s, v) ==> u <= v
  }

  /** Every nonempty set of reals that is bounded above has a least upper bound. */
  lemma ExistsLub(s: iset<real>)
    requires Eligible(s)
    ensures exists u :: IsLub(s, u)
  {
    var w :| w in s;
    var b :| upper(s, b);
    var u := Complete(s, w, b);
    assert IsLub(s, u);
  }

  /** A set has at most one least upper bound. */
  lemma UniqueLub(s: iset<real>, u: real, v: real)
    requires IsLub(s, u)
    requires IsLub(s, v)
    ensures u == v
  {
    assert upper(s, u);
    assert upper(s, v);
  }

  /**
   The least upper bound of `s` when `s` is nonempty and bounded above,
   and `0.0` otherwise.
   */
  ghost function Sup(s: iset<real>): (u: real)
    ensures Eligible(s) ==> IsLub(s, u)
    ensures !Eligible(s) ==> u == 0.0
  {
    if Eligible(s) then
      ExistsLub(s);
      var v :| IsLub(s, v);
      v
    else
      0.0
  }
}
