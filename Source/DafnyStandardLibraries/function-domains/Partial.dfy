/*******************************************************************************
 * Copyright by the contributors to the Dafny Project
 * SPDX-License-Identifier: MIT
 *******************************************************************************/

/** Extensional equality of heap-independent partial functions.
    Verify this source with --additional-axioms. */
module Std.PartialFunctionDomains {
  module Arity0 {

    /** Preserve the precondition and defined results on exactly this product. */
    ghost function {:transparent} {:functionDomain "partialRestrict"} Restrict<B>(f: () --> B): () --> B {
      () requires f.requires() => f()
    }

    lemma Restriction<B>(f: () --> B)
      ensures ExactDom(Restrict(f))
      ensures ExactDom(f) <==> Restrict(f) == f
      ensures Pointwise(Restrict(f), f)
    {}
    ghost predicate {:transparent} {:functionDomain "partialExactDom"} ExactDom<B>(f: () --> B)

    /** Equal preconditions on every correctly typed tuple, and equal results
        wherever they hold. This includes unallocated arguments. */
    ghost predicate {:transparent} {:functionDomain "partialPointwise"} Pointwise<B>(f: () --> B, g: () --> B)

    ghost predicate {:transparent} {:no_inline} {:functionDomain "partialEqual"} Equal<B>(f: () --> B, g: () --> B) { f == g }

    lemma Extensionality<B>(f: () --> B, g: () --> B)
      requires ExactDom(f) && ExactDom(g)
      requires Pointwise(f, g)
      ensures f == g
    {
      assert Equal(f, g);
    }
  }
  module Arity1 {

    /** Preserve the precondition and defined results on exactly this product. */
    ghost function {:transparent} {:functionDomain "partialRestrict"} Restrict<A0, B>(f: A0 --> B): A0 --> B {
      (x0: A0) requires f.requires(x0) => f(x0)
    }

    lemma Restriction<A0, B>(f: A0 --> B)
      ensures ExactDom(Restrict(f))
      ensures ExactDom(f) <==> Restrict(f) == f
      ensures Pointwise(Restrict(f), f)
    {}
    ghost predicate {:transparent} {:functionDomain "partialExactDom"} ExactDom<A0, B>(f: A0 --> B)

    /** Equal preconditions on every correctly typed tuple, and equal results
        wherever they hold. This includes unallocated arguments. */
    ghost predicate {:transparent} {:functionDomain "partialPointwise"} Pointwise<A0, B>(f: A0 --> B, g: A0 --> B)

    ghost predicate {:transparent} {:no_inline} {:functionDomain "partialEqual"} Equal<A0, B>(f: A0 --> B, g: A0 --> B) { f == g }

    lemma Extensionality<A0, B>(f: A0 --> B, g: A0 --> B)
      requires ExactDom(f) && ExactDom(g)
      requires Pointwise(f, g)
      ensures f == g
    {
      assert Equal(f, g);
    }
  }
  module Arity2 {

    /** Preserve the precondition and defined results on exactly this product. */
    ghost function {:transparent} {:functionDomain "partialRestrict"} Restrict<A0, A1, B>(f: (A0, A1) --> B): (A0, A1) --> B {
      (x0: A0, x1: A1) requires f.requires(x0, x1) => f(x0, x1)
    }

    lemma Restriction<A0, A1, B>(f: (A0, A1) --> B)
      ensures ExactDom(Restrict(f))
      ensures ExactDom(f) <==> Restrict(f) == f
      ensures Pointwise(Restrict(f), f)
    {}
    ghost predicate {:transparent} {:functionDomain "partialExactDom"} ExactDom<A0, A1, B>(f: (A0, A1) --> B)

    /** Equal preconditions on every correctly typed tuple, and equal results
        wherever they hold. This includes unallocated arguments. */
    ghost predicate {:transparent} {:functionDomain "partialPointwise"} Pointwise<A0, A1, B>(f: (A0, A1) --> B, g: (A0, A1) --> B)

    ghost predicate {:transparent} {:no_inline} {:functionDomain "partialEqual"} Equal<A0, A1, B>(f: (A0, A1) --> B, g: (A0, A1) --> B) { f == g }

    lemma Extensionality<A0, A1, B>(f: (A0, A1) --> B, g: (A0, A1) --> B)
      requires ExactDom(f) && ExactDom(g)
      requires Pointwise(f, g)
      ensures f == g
    {
      assert Equal(f, g);
    }
  }
  module Arity3 {

    /** Preserve the precondition and defined results on exactly this product. */
    ghost function {:transparent} {:functionDomain "partialRestrict"} Restrict<A0, A1, A2, B>(f: (A0, A1, A2) --> B): (A0, A1, A2) --> B {
      (x0: A0, x1: A1, x2: A2) requires f.requires(x0, x1, x2) => f(x0, x1, x2)
    }

    lemma Restriction<A0, A1, A2, B>(f: (A0, A1, A2) --> B)
      ensures ExactDom(Restrict(f))
      ensures ExactDom(f) <==> Restrict(f) == f
      ensures Pointwise(Restrict(f), f)
    {}
    ghost predicate {:transparent} {:functionDomain "partialExactDom"} ExactDom<A0, A1, A2, B>(f: (A0, A1, A2) --> B)

    /** Equal preconditions on every correctly typed tuple, and equal results
        wherever they hold. This includes unallocated arguments. */
    ghost predicate {:transparent} {:functionDomain "partialPointwise"} Pointwise<A0, A1, A2, B>(f: (A0, A1, A2) --> B, g: (A0, A1, A2) --> B)

    ghost predicate {:transparent} {:no_inline} {:functionDomain "partialEqual"} Equal<A0, A1, A2, B>(f: (A0, A1, A2) --> B, g: (A0, A1, A2) --> B) { f == g }

    lemma Extensionality<A0, A1, A2, B>(f: (A0, A1, A2) --> B, g: (A0, A1, A2) --> B)
      requires ExactDom(f) && ExactDom(g)
      requires Pointwise(f, g)
      ensures f == g
    {
      assert Equal(f, g);
    }
  }
  module Arity4 {

    /** Preserve the precondition and defined results on exactly this product. */
    ghost function {:transparent} {:functionDomain "partialRestrict"} Restrict<A0, A1, A2, A3, B>(f: (A0, A1, A2, A3) --> B): (A0, A1, A2, A3) --> B {
      (x0: A0, x1: A1, x2: A2, x3: A3) requires f.requires(x0, x1, x2, x3) => f(x0, x1, x2, x3)
    }

    lemma Restriction<A0, A1, A2, A3, B>(f: (A0, A1, A2, A3) --> B)
      ensures ExactDom(Restrict(f))
      ensures ExactDom(f) <==> Restrict(f) == f
      ensures Pointwise(Restrict(f), f)
    {}
    ghost predicate {:transparent} {:functionDomain "partialExactDom"} ExactDom<A0, A1, A2, A3, B>(f: (A0, A1, A2, A3) --> B)

    /** Equal preconditions on every correctly typed tuple, and equal results
        wherever they hold. This includes unallocated arguments. */
    ghost predicate {:transparent} {:functionDomain "partialPointwise"} Pointwise<A0, A1, A2, A3, B>(f: (A0, A1, A2, A3) --> B, g: (A0, A1, A2, A3) --> B)

    ghost predicate {:transparent} {:no_inline} {:functionDomain "partialEqual"} Equal<A0, A1, A2, A3, B>(f: (A0, A1, A2, A3) --> B, g: (A0, A1, A2, A3) --> B) { f == g }

    lemma Extensionality<A0, A1, A2, A3, B>(f: (A0, A1, A2, A3) --> B, g: (A0, A1, A2, A3) --> B)
      requires ExactDom(f) && ExactDom(g)
      requires Pointwise(f, g)
      ensures f == g
    {
      assert Equal(f, g);
    }
  }
}
