/*******************************************************************************
 * Copyright by the contributors to the Dafny Project
 * SPDX-License-Identifier: MIT
 *******************************************************************************/

include "Functions.dfy"

/** Allocation-independent pointwise agreement for total arrows, including domains
    that may contain references. Verify this source with --additional-axioms. */
module Std.FunctionPointwise {

  module Arity1 {
    import F = FunctionDomains.Arity1

    /** Agreement on every correctly typed argument tuple, including unallocated
        values. The compiler defines this quantification without a heap argument. */
    ghost predicate {:transparent} {:functionDomain "pointwise"} Pointwise<A0, B>(f: A0 -> B, g: A0 -> B)

    /** An explicit checked call, with no (!new) restriction on the domain. */
    lemma Extensionality<A0, B>(f: A0 -> B, g: A0 -> B)
      requires F.ExactDom(f) && F.ExactDom(g)
      requires Pointwise(f, g)
      ensures f == g
    {
      assert F.Equal(f, g);
    }
  }
  module Arity2 {
    import F = FunctionDomains.Arity2

    /** Agreement on every correctly typed argument tuple, including unallocated
        values. The compiler defines this quantification without a heap argument. */
    ghost predicate {:transparent} {:functionDomain "pointwise"} Pointwise<A0, A1, B>(f: (A0, A1) -> B, g: (A0, A1) -> B)

    /** An explicit checked call, with no (!new) restriction on the domain. */
    lemma Extensionality<A0, A1, B>(f: (A0, A1) -> B, g: (A0, A1) -> B)
      requires F.ExactDom(f) && F.ExactDom(g)
      requires Pointwise(f, g)
      ensures f == g
    {
      assert F.Equal(f, g);
    }
  }
  module Arity3 {
    import F = FunctionDomains.Arity3

    /** Agreement on every correctly typed argument tuple, including unallocated
        values. The compiler defines this quantification without a heap argument. */
    ghost predicate {:transparent} {:functionDomain "pointwise"} Pointwise<A0, A1, A2, B>(f: (A0, A1, A2) -> B, g: (A0, A1, A2) -> B)

    /** An explicit checked call, with no (!new) restriction on the domain. */
    lemma Extensionality<A0, A1, A2, B>(f: (A0, A1, A2) -> B, g: (A0, A1, A2) -> B)
      requires F.ExactDom(f) && F.ExactDom(g)
      requires Pointwise(f, g)
      ensures f == g
    {
      assert F.Equal(f, g);
    }
  }
  module Arity4 {
    import F = FunctionDomains.Arity4

    /** Agreement on every correctly typed argument tuple, including unallocated
        values. The compiler defines this quantification without a heap argument. */
    ghost predicate {:transparent} {:functionDomain "pointwise"} Pointwise<A0, A1, A2, A3, B>(f: (A0, A1, A2, A3) -> B, g: (A0, A1, A2, A3) -> B)

    /** An explicit checked call, with no (!new) restriction on the domain. */
    lemma Extensionality<A0, A1, A2, A3, B>(f: (A0, A1, A2, A3) -> B, g: (A0, A1, A2, A3) -> B)
      requires F.ExactDom(f) && F.ExactDom(g)
      requires Pointwise(f, g)
      ensures f == g
    {
      assert F.Equal(f, g);
    }
  }
}
