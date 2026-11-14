/*******************************************************************************
 *  Copyright by the contributors to the Dafny Project
 *  SPDX-License-Identifier: MIT
 *******************************************************************************/

/**
 Trusted facts that are true in Dafny's intended model but cannot be proved
 from its axiomatization.

 This module is the one place in the standard library where such facts are
 stated.  Each is a lemma with the attribute `{:axiom}` and no body.  A lemma
 without a body is a procedure, not a global axiom: its conclusions are
 available only at an explicit call, and only for the arguments of that call.
 A program that does not call one of these lemmas, directly or through a
 verified wrapper such as `Std.RealCompleteness`, is unaffected by it, and
 importing this module adds nothing to the verifier's context.

 Every lemma's documentation states the mathematical fact, why it is true in
 Dafny's intended model, and the restrictions that keep it sound against the
 way Dafny encodes its types for the verifier.  `dafny audit` lists each of
 them as an assumption.
 */
module Std.Axioms {

  /**
   Function extensionality: two total functions that agree at every argument
   are equal.

   In Dafny's intended model a value of a total arrow type `A -> B` is a
   mathematical function from `A` to `B`: it has no precondition, reads
   nothing, and so is determined by its results.  Dafny's verifier, however,
   represents a function value by a handle with separate application,
   precondition and reads parts, each defined for every heap, every type index
   and every boxed argument, including arguments that are not values of `A`.
   Two handles that agree on `A` can still differ outside `A`, and the
   verifier can sometimes see that difference.  So the fact is stated here only
   for the domains where it cannot:

   - `int`, `real`, `bool` and `ORDINAL`: every value of the verifier's
     representation of these types is a value of the Dafny type (it satisfies
     `$Is`), so a function value's behaviour at any boxed argument is its
     behaviour at some value of the domain; and none of these types has a
     proper supertype, so no wider arrow type can view the same handle at
     arguments outside the domain;
   - the empty domain of `() -> B`, and the pair domain `(int, int) -> B`.

   It is *not* stated for an arbitrary type parameter `A`, because then it
   would be unsound:

   - **Subset types.**  `nat -> int` and `int ~> int` share their base type, so
     a `nat -> int` value can be viewed as an `int ~> int` and applied to `-1`.
     With `f := (x: int) => if x < 0 then 7 else x` and `g := (x: int) => x`,
     both of type `nat -> int`, a generic lemma would give `f == g` and then
     `7 == -1`; the regression test `git-issues/github-issue-34.dfy` records
     that this proves `false`.  A type parameter can be instantiated with a
     subset type, so no generic form is safe.
   - **Datatypes, `char`, sequences, sets and other types whose representation
     contains values outside the type.**  A lambda's application is defined at
     every boxed argument, through an unbox to the representation type.  Two
     lambdas over a datatype `D` that agree on every `D` can disagree on a
     value of another datatype, so equating them contradicts the verifier's
     own axioms.
   - **Partial (`-->`) and heap-reading (`~>`) arrows.**  Their values also
     carry a precondition and a reads set, which pointwise agreement of results
     does not determine, and a lambda's results outside its precondition are
     still visible to the verifier.

   Bitvector domains are sound for the same reason as `int`, but would need one
   lemma per width; they are not provided.  Other domains can be reached
   through these: for example, an `int -> B` value restricted to `nat` is
   still an `int -> B` value, and two of them are equal when they agree on all
   of `int`.

   The result type `B` is unrestricted: results are compared with `==` at `B`,
   and a wider view of the result type sees the same values.
   */
  module FunctionExtensionality {

    /** Two total nullary functions with the same result are equal. */
    lemma {:axiom} Nullary<B>(f: () -> B, g: () -> B)
      requires f() == g()
      ensures f == g

    /** Two total functions on `int` that agree at every integer are equal. */
    lemma {:axiom} OnInt<B>(f: int -> B, g: int -> B)
      requires forall x: int :: f(x) == g(x)
      ensures f == g

    /** Two total functions on `real` that agree at every real are equal. */
    lemma {:axiom} OnReal<B>(f: real -> B, g: real -> B)
      requires forall x: real :: f(x) == g(x)
      ensures f == g

    /** Two total functions on `bool` that agree at `false` and `true` are equal. */
    lemma {:axiom} OnBool<B>(f: bool -> B, g: bool -> B)
      requires forall x: bool :: f(x) == g(x)
      ensures f == g

    /** Two total functions on `ORDINAL` that agree at every ordinal are equal. */
    lemma {:axiom} OnOrdinal<B>(f: ORDINAL -> B, g: ORDINAL -> B)
      requires forall x: ORDINAL :: f(x) == g(x)
      ensures f == g

    /** Two total functions of two integers that agree at every pair are equal. */
    lemma {:axiom} OnIntInt<B>(f: (int, int) -> B, g: (int, int) -> B)
      requires forall x: int, y: int :: f(x, y) == g(x, y)
      ensures f == g
  }

  /**
   Completeness of `real`.

   Dafny axiomatizes `real` as an ordered field, with `.Floor` and the
   embedding of the integers, but not as a complete one, and completeness does
   not follow from the ordered-field laws (the real algebraic numbers are a
   model of them).  Its intended model is the real numbers ℝ, which are
   complete: every nonempty set of reals that is bounded above has a least
   upper bound.  `iset<real>` ranges over arbitrary sets of reals, so the fact
   is stated for those.

   `Std.RealCompleteness` builds the usual interface (`Sup`, `ExistsLub`) on
   this lemma.
   */
  module Reals {

    /** `b` is an upper bound of `s`. */
    ghost predicate UpperBound(s: iset<real>, b: real) {
      forall x {:trigger x in s} :: x in s ==> x <= b
    }

    /**
     Every nonempty set of reals that is bounded above has a least upper
     bound.

     `Complete` has no preconditions, so that a call is never itself a proof
     obligation.  Its conclusions are guarded instead: they hold only when `w`
     is a member of `s` and `b` is an upper bound of `s`.  For any other
     arguments (an empty set, an unbounded set, a witness that is not a
     member) the result is an unspecified real about which nothing is known.
     Without the guard the lemma would be false, for example of the empty set,
     which has no least upper bound.
     */
    lemma {:axiom} Complete(s: iset<real>, w: real, b: real) returns (u: real)
      ensures w in s && UpperBound(s, b) ==> UpperBound(s, u)
      ensures w in s && UpperBound(s, b) ==>
                forall v {:trigger UpperBound(s, v)} :: UpperBound(s, v) ==> u <= v
  }
}
