/*******************************************************************************
 * Copyright by the contributors to the Dafny Project
 * SPDX-License-Identifier: MIT
 *******************************************************************************/

// Source library for the opt-in function-domain encoding (issue 374).
// Verify this file and its clients with --additional-axioms or
// --function-extensionality-axiom. No trusted source declarations are needed.
module Std.FunctionDomains {

  module Arity0 {
    /** The function with exactly this product domain and the same results as f. */
    ghost function {:transparent} {:functionDomain "restrict"} Restrict<B>(f: () -> B): () -> B {
      () => f()
    }

    /** Whether the value's domain is exactly this product, rather than a superset. */
    ghost predicate {:transparent} {:functionDomain "exactDom"} ExactDom<B>(f: () -> B)

    // An explicit instance of extensionality. The intrinsic checks this body and
    // signature; the predicate itself is ordinary equality, with a checked body.
    ghost predicate {:transparent} {:no_inline} {:functionDomain "equal"} Equal<B>(f: () -> B, g: () -> B) {
      f == g
    }

    lemma Restriction<B>(f: () -> B)
      ensures ExactDom(Restrict(f))
      ensures ExactDom(f) <==> Restrict(f) == f
      ensures Restrict(f)() == f()
    {}

    /** Explicit-call extensionality; no automatic equality trigger is required. */
    lemma Extensionality<B>(f: () -> B, g: () -> B)
      requires ExactDom(f) && ExactDom(g)
      requires f() == g()
      ensures f == g
    {
      assert Equal(f, g);
    }
  }

  module Arity1 {
    /** The function with exactly this product domain and the same results as f. */
    ghost function {:transparent} {:functionDomain "restrict"} Restrict<A0, B>(f: A0 -> B): A0 -> B {
      (x0: A0) => f(x0)
    }

    /** Whether the value's domain is exactly this product, rather than a superset. */
    ghost predicate {:transparent} {:functionDomain "exactDom"} ExactDom<A0, B>(f: A0 -> B)

    // An explicit instance of extensionality. The intrinsic checks this body and
    // signature; the predicate itself is ordinary equality, with a checked body.
    ghost predicate {:transparent} {:no_inline} {:functionDomain "equal"} Equal<A0, B>(f: A0 -> B, g: A0 -> B) {
      f == g
    }

    lemma Restriction<A0, B>(f: A0 -> B)
      ensures ExactDom(Restrict(f))
      ensures ExactDom(f) <==> Restrict(f) == f
      ensures forall x0: A0 :: Restrict(f)(x0) == f(x0)
    {}

    /** Explicit-call extensionality; no automatic equality trigger is required. */
    lemma Extensionality<A0(!new), B>(f: A0 -> B, g: A0 -> B)
      requires ExactDom(f) && ExactDom(g)
      requires forall x0: A0 :: f(x0) == g(x0)
      ensures f == g
    {
      assert Equal(f, g);
    }
  }

  module Arity2 {
    /** The function with exactly this product domain and the same results as f. */
    ghost function {:transparent} {:functionDomain "restrict"} Restrict<A0, A1, B>(f: (A0, A1) -> B): (A0, A1) -> B {
      (x0: A0, x1: A1) => f(x0, x1)
    }

    /** Whether the value's domain is exactly this product, rather than a superset. */
    ghost predicate {:transparent} {:functionDomain "exactDom"} ExactDom<A0, A1, B>(f: (A0, A1) -> B)

    // An explicit instance of extensionality. The intrinsic checks this body and
    // signature; the predicate itself is ordinary equality, with a checked body.
    ghost predicate {:transparent} {:no_inline} {:functionDomain "equal"} Equal<A0, A1, B>(f: (A0, A1) -> B, g: (A0, A1) -> B) {
      f == g
    }

    lemma Restriction<A0, A1, B>(f: (A0, A1) -> B)
      ensures ExactDom(Restrict(f))
      ensures ExactDom(f) <==> Restrict(f) == f
      ensures forall x0: A0, x1: A1 :: Restrict(f)(x0, x1) == f(x0, x1)
    {}

    /** Explicit-call extensionality; no automatic equality trigger is required. */
    lemma Extensionality<A0(!new), A1(!new), B>(f: (A0, A1) -> B, g: (A0, A1) -> B)
      requires ExactDom(f) && ExactDom(g)
      requires forall x0: A0, x1: A1 :: f(x0, x1) == g(x0, x1)
      ensures f == g
    {
      assert Equal(f, g);
    }
  }

  module Arity3 {
    /** The function with exactly this product domain and the same results as f. */
    ghost function {:transparent} {:functionDomain "restrict"} Restrict<A0, A1, A2, B>(f: (A0, A1, A2) -> B): (A0, A1, A2) -> B {
      (x0: A0, x1: A1, x2: A2) => f(x0, x1, x2)
    }

    /** Whether the value's domain is exactly this product, rather than a superset. */
    ghost predicate {:transparent} {:functionDomain "exactDom"} ExactDom<A0, A1, A2, B>(f: (A0, A1, A2) -> B)

    // An explicit instance of extensionality. The intrinsic checks this body and
    // signature; the predicate itself is ordinary equality, with a checked body.
    ghost predicate {:transparent} {:no_inline} {:functionDomain "equal"} Equal<A0, A1, A2, B>(f: (A0, A1, A2) -> B, g: (A0, A1, A2) -> B) {
      f == g
    }

    lemma Restriction<A0, A1, A2, B>(f: (A0, A1, A2) -> B)
      ensures ExactDom(Restrict(f))
      ensures ExactDom(f) <==> Restrict(f) == f
      ensures forall x0: A0, x1: A1, x2: A2 :: Restrict(f)(x0, x1, x2) == f(x0, x1, x2)
    {}

    /** Explicit-call extensionality; no automatic equality trigger is required. */
    lemma Extensionality<A0(!new), A1(!new), A2(!new), B>(f: (A0, A1, A2) -> B, g: (A0, A1, A2) -> B)
      requires ExactDom(f) && ExactDom(g)
      requires forall x0: A0, x1: A1, x2: A2 :: f(x0, x1, x2) == g(x0, x1, x2)
      ensures f == g
    {
      assert Equal(f, g);
    }
  }

  module Arity4 {
    /** The function with exactly this product domain and the same results as f. */
    ghost function {:transparent} {:functionDomain "restrict"} Restrict<A0, A1, A2, A3, B>(f: (A0, A1, A2, A3) -> B): (A0, A1, A2, A3) -> B {
      (x0: A0, x1: A1, x2: A2, x3: A3) => f(x0, x1, x2, x3)
    }

    /** Whether the value's domain is exactly this product, rather than a superset. */
    ghost predicate {:transparent} {:functionDomain "exactDom"} ExactDom<A0, A1, A2, A3, B>(f: (A0, A1, A2, A3) -> B)

    // An explicit instance of extensionality. The intrinsic checks this body and
    // signature; the predicate itself is ordinary equality, with a checked body.
    ghost predicate {:transparent} {:no_inline} {:functionDomain "equal"} Equal<A0, A1, A2, A3, B>(f: (A0, A1, A2, A3) -> B, g: (A0, A1, A2, A3) -> B) {
      f == g
    }

    lemma Restriction<A0, A1, A2, A3, B>(f: (A0, A1, A2, A3) -> B)
      ensures ExactDom(Restrict(f))
      ensures ExactDom(f) <==> Restrict(f) == f
      ensures forall x0: A0, x1: A1, x2: A2, x3: A3 :: Restrict(f)(x0, x1, x2, x3) == f(x0, x1, x2, x3)
    {}

    /** Explicit-call extensionality; no automatic equality trigger is required. */
    lemma Extensionality<A0(!new), A1(!new), A2(!new), A3(!new), B>(f: (A0, A1, A2, A3) -> B, g: (A0, A1, A2, A3) -> B)
      requires ExactDom(f) && ExactDom(g)
      requires forall x0: A0, x1: A1, x2: A2, x3: A3 :: f(x0, x1, x2, x3) == g(x0, x1, x2, x3)
      ensures f == g
    {
      assert Equal(f, g);
    }
  }
}
