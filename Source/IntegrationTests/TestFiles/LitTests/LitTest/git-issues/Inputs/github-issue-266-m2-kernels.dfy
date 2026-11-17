// The divergence kernels K1, K2, K4, K5 and K6 of docs/dev/design/266-bounded-callables.md, section 5.  Each wrong rule
// would admit a call cycle through a stored bounded value; each must fail at the named obligation.

// K1: equality transfer, wrapping and narrowing.  The trace would be high(2) -> C.f(1) -> h(0) -> high(2) -> ...
module K1A {
  trait {:termination false} {:termination_closed} T extends object {
    function f(): int
      ensures f() == 0
      decreases {:global} 1
    ghost function g(): int
      ensures g() == 0
      decreases {:global} 1
  }
  function high(t: T): int
    ensures high(t) == 0
    decreases {:global} 2
  {
    t.f()
  }
  ghost function ghostHigh(t: T): int
    ensures ghostHigh(t) == 0
    decreases {:global} 2
  {
    t.g()
  }
}
module K1B {
  import A = K1A
  type Zero = h: bounded<nat, A.T -> int> | h.bound == 0
    ghost witness bounded.certify(0, (t: A.T) => 0)
  class C extends A.T {
    const cb: Zero
    ghost const gcb: Zero
    constructor (h: Zero, ghost g: Zero) { cb := h; gcb := g; }
    function f(): int
      ensures f() == 0
    {
      var ignored := cb(this);
      0
    }
    ghost function g(): int
      ensures g() == 0
    {
      var ignored := gcb(this);
      0
    }
  }
  // The named function runs its own code, under bound 2.
  method Known() {
    var good := bounded.certify(0, (t: A.T) => 0);
    assert forall t: A.T :: A.high(t) == good(t);
    var h: Zero := bounded.certify(0, A.high); // error: 2 <= 0
    var c := new C(h, h);
    var result := A.high(c);
  }
  method Narrow() {
    var h2 := bounded.certify(2, A.high);
    var h: Zero := bounded.certify(0, h2.erase); // error: 2 <= 0
  }
  method Wrap() {
    var h: Zero := bounded.certify(0, (t: A.T) => A.high(t)); // error: 2 <= 0
  }
  lemma Forge() returns (h: Zero) {
    h := bounded.certify(0, A.ghostHigh); // error: 2 <= 0
  }
}

// K2: non-strict application.  If C.f could apply cb at its own bound, high -> f -> cb -> high would loop.
module K2 {
  type Zero = h: bounded<nat, C -> int> | h.bound == 0
    ghost witness bounded.certify(0, (c: C) => 0)
  class C {
    const cb: Zero
    constructor (h: Zero) { cb := h; }
    function f(): int
      decreases {:global} 0
    {
      cb(this) // error: 0 < 0
    }
  }
  function high(c: C): int
    decreases {:global} 0
  {
    c.f()
  }
  method Main() {
    var h: Zero := bounded.certify(0, high);
    var c := new C(h);
    var result := high(c);
  }
}

// K4: the precondition of a lambda runs under its bound too, before it is known to hold.
module K4 {
  trait {:termination false} T extends object {
    ghost predicate p()
      decreases {:global} 1
  }
  ghost predicate high(t: T)
    decreases {:global} 2
  {
    t.p()
  }
  type Zero = h: bounded<nat, T --> int> | h.bound == 0
    ghost witness bounded.certify(0, (t: T) => 0)
  class C extends T {
    ghost const cb: Zero
    constructor (ghost h: Zero) { cb := h; }
    ghost predicate p() { cb.requires(this) }
  }
  method Main() {
    ghost var cb: Zero := bounded.certify(0, (t: T) requires high(t) => 0); // error: 2 <= 0
    var c := new C(cb);
    ghost var result := cb.requires(c);
  }
  method AndFalse() {
    ghost var cb: Zero := bounded.certify(0, (t: T) requires high(t) && false => 0); // error: 2 <= 0
  }
}

// K5: the frame of a lambda runs under its bound too.
module K5 {
  trait {:termination false} T extends object {
    ghost function r(): set<object?>
      reads *
      decreases {:global} 1
  }
  ghost function highReads(t: T): set<object?>
    reads *
    decreases {:global} 2
  {
    t.r()
  }
  type Zero = h: bounded<nat, T ~> int> | h.bound == 0
    ghost witness bounded.certify(0, (t: T) => 0)
  class C extends T {
    ghost const cb: Zero
    constructor (ghost h: Zero) { cb := h; }
    ghost function r(): set<object?>
      reads *
    {
      if cb.requires(this) then cb.reads(this) else {}
    }
  }
  method Main() {
    ghost var cb: Zero := bounded.certify(0, (t: T) reads highReads(t) => 0); // error: 2 <= 0
    var c := new C(cb);
    ghost var footprint := cb.reads(c);
  }
}

// K6: the bound is a snapshot.  If it tracked the live ghost k, h would become a Zero and K1's loop would return.
module K6A {
  trait {:termination false} {:termination_closed} T extends object {
    function f(): int
      decreases {:global} 1
  }
  function high(t: T): int
    decreases {:global} 2
  {
    t.f()
  }
}
module K6B {
  import A = K6A
  type Zero = h: bounded<nat, A.T -> int> | h.bound == 0
    ghost witness bounded.certify(0, (t: A.T) => 0)
  class C extends A.T {
    const cb: Zero
    constructor (h: Zero) { cb := h; }
    function f(): int { cb(this) }
  }
  method Main() {
    ghost var k: nat := 2;
    var h := bounded.certify(k, A.high);
    k := 0;
    var c := new C(h); // error: h.bound is 2, not 0
  }
}
