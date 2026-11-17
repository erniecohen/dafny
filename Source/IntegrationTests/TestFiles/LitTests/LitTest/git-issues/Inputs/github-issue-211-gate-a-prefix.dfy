// Implementations of the lemma slot W.P that call the least lemma Walk, or its prefix lemma, of the library.
// C and D claim false and call Walk under a larger bound than the slot has: if either were accepted, P(0)
// would prove false.  E calls it within the rules.
module Client {
  import opened Lib
  class C extends W {
    constructor() {}
    ghost predicate Claim(n: nat) { false }
    lemma P(n: nat)
      ensures Claim(n)
    {
      assert Even(0);
      Walk(this, n + 1, 0);
    }
  }
  class D extends W {
    constructor() {}
    ghost predicate Claim(n: nat) { false }
    lemma P(n: nat)
      ensures Claim(n)
    {
      assert Even#[1](0);
      Walk#[1](this, n + 1, 0);
    }
  }
  class E extends W {
    constructor() {}
    ghost predicate Claim(n: nat) { true }
    lemma P(n: nat)
      ensures Claim(n)
    {
      assert Even(0);
      Walk(this, n, 0);
      assert false; // control: must fail
    }
  }
  lemma False(c: C, d: D)
    ensures false
  {
    c.P(0);
  }
}
