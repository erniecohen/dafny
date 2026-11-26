/*******************************************************************************
 * Copyright by the contributors to the Dafny Project
 * SPDX-License-Identifier: MIT
 *******************************************************************************/

/** Extensional equality of heap-independent partial functions.
    Verify this source with --additional-axioms. */
module Std.PartialFunctionDomains {
  module Arity0 {
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
