// Kernel K5 of docs/dev/design/266-bounded-callables.md, section 5: the frame of a lambda runs under its bound too.
// Its class keeps a bounded value whose function reads the heap, through the subset type Zero.  Milestone 2 does
// not let a field hold one: the rule against storing functions that read the heap looks through Zero to the
// function (https://github.com/erniecohen/dafny/issues/479), so the kernel is rejected at resolution.  Milestone 3
// admits the field, and the kernel then fails at its intended obligation, the bound of the lambda's frame.

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
