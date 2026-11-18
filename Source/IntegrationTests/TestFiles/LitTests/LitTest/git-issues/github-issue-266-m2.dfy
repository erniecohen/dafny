// https://github.com/erniecohen/dafny/issues/266, milestone 2: the certification kernel
// Under --modular-termination, "bounded.certify(a, f)" makes a value of type bounded<O, F> whose function "f" runs
// under the bound "a": a lambda's body is checked under "a", the function of a bounded value must have a bound at
// most "a", and a named function, which runs its own code, must have a 'decreases {:global}' bound at most "a".
// Applying the value (the call sugar, "erase", "requires", "reads") needs its bound below the bound of the code
// that applies it.  A constant field can hold one; storing into mutable fields, collections and datatypes, and
// returning one, are milestone 3.
// RUN: echo "positive, both resolvers" > "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --warn-unchecked-termination=false "%s" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --warn-unchecked-termination=false --type-system-refresh --general-traits=datatype "%s" >> "%t"
// RUN: echo "false controls: one reachable assert false per family, both resolvers" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --warn-unchecked-termination=false "%S/Inputs/github-issue-266-m2-false.dfy" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --warn-unchecked-termination=false --type-system-refresh --general-traits=datatype "%S/Inputs/github-issue-266-m2-false.dfy" >> "%t"
// RUN: echo "divergence kernels, both resolvers" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --warn-unchecked-termination=false "%S/Inputs/github-issue-266-m2-kernels.dfy" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --warn-unchecked-termination=false --type-system-refresh --general-traits=datatype "%S/Inputs/github-issue-266-m2-kernels.dfy" >> "%t"
// RUN: echo "kernel K5: milestone 3 admits its field, and it fails at its obligation, both resolvers" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --warn-unchecked-termination=false "%S/Inputs/github-issue-266-m2-k5.dfy" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --warn-unchecked-termination=false --type-system-refresh --general-traits=datatype "%S/Inputs/github-issue-266-m2-k5.dfy" >> "%t"
// RUN: echo "rule violations, both resolvers" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --warn-unchecked-termination=false "%S/Inputs/github-issue-266-m2-reject.dfy" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --warn-unchecked-termination=false --type-system-refresh --general-traits=datatype "%S/Inputs/github-issue-266-m2-reject.dfy" >> "%t"
// RUN: echo "values that cannot be certified, both resolvers" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --warn-unchecked-termination=false "%S/Inputs/github-issue-266-m2-certify.dfy" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --warn-unchecked-termination=false --type-system-refresh --general-traits=datatype "%S/Inputs/github-issue-266-m2-certify.dfy" >> "%t"
// RUN: echo "ghost errors, both resolvers" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --warn-unchecked-termination=false "%S/Inputs/github-issue-266-m2-resolve.dfy" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --warn-unchecked-termination=false --type-system-refresh --general-traits=datatype "%S/Inputs/github-issue-266-m2-resolve.dfy" >> "%t"
// RUN: echo "knots through data, both resolvers" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --warn-unchecked-termination=false "%S/Inputs/github-issue-266-m2-datatype.dfy" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --warn-unchecked-termination=false --type-system-refresh --general-traits=datatype "%S/Inputs/github-issue-266-m2-datatype.dfy" >> "%t"
// RUN: echo "knots through the parameter and result types of a bounded function, both resolvers" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --warn-unchecked-termination=false "%S/Inputs/github-issue-266-m2-wrapper.dfy" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --warn-unchecked-termination=false --type-system-refresh --general-traits=datatype "%S/Inputs/github-issue-266-m2-wrapper.dfy" >> "%t"
// RUN: echo "a stored bounded value, compiled" >> "%t"
// RUN: %exits-with 0 %baredafny run --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --warn-unchecked-termination=false --target cs "%S/Inputs/github-issue-266-m2-run.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Applications under a global bound, by the call sugar, "erase", "requires" and "reads".
module Apply {
  function Apply3(c: bounded<nat, int -> int>, n: nat): int
    requires c.bound < n
    decreases {:global} n
  {
    c(3) + c.erase(3)
  }
  // Code without a bound is above every bound.
  function ApplyTop(c: bounded<nat, int -> int>): int {
    c(3)
  }
  ghost function Reads(c: bounded<nat, int ~> int>, n: nat): set<object?>
    requires c.bound < n && c.requires(3)
    reads c.reads(3)
    decreases {:global} n
  {
    c.reads(3)
  }
  method Method(c: bounded<ORDINAL, int -> int>, o: ORDINAL) returns (r: int)
    requires c.bound < o
    decreases {:global} o
  {
    r := c(3);
  }
}

// Formation from a lambda, from a bounded value (copy or widen), and from a named function.
module Form {
  import opened Apply
  method Lambda() returns (r: int) {
    var d := bounded.certify(5, (x: int) requires x > 0 => x + 1);
    assert d.bound == 5 && d.requires(1) && d(2) == 3;
    r := d(2);
    var o := bounded.certify(7 as ORDINAL, (x: int) => x);
    var p: bounded<ORDINAL, int -> int> := o;
    assert p.bound == 7 as ORDINAL;
  }
  function Widen(c: bounded<nat, int -> int>): bounded<nat, int -> int>
    requires c.bound <= 7
  {
    bounded.certify(7, c.erase)
  }
  // A lambda applies a captured value under its own bound.
  function Nest(c: bounded<nat, int -> int>): bounded<nat, int -> int>
    requires c.bound < 9
  {
    bounded.certify(9, x => c(x) + 1)
  }
  // A lambda calls a function at its own bound (same-bound rule).
  function G(n: nat): int
    decreases {:global} 5
  {
    if n == 0 then 0 else Apply3(bounded.certify(3, x => x), 4)
  }
  // Forwarding to a parameter with a callback bound.
  function Use(f: int -> int, k: nat): int
    decreases {:callback f, k}
  {
    0
  }
  function Forward(c: bounded<nat, int -> int>): int
    requires c.bound <= 4
  {
    Use(c.erase, 4)
  }
}

// A named function is admitted with its own code: its bound must not exceed the bound given.
module Named {
  trait {:termination false} T extends object {
    function f(): int
      decreases {:global} 1
  }
  function High(t: T): int
    decreases {:global} 2
  {
    t.f()
  }
  function Leaf(t: T): int {
    0
  }
  class K {
    const n: int
    function Get(t: T): int
      decreases {:global} 3
    {
      t.f() + n
    }
  }
  method Certify(k: K) {
    var h := bounded.certify(2, High);
    var g := bounded.certify(4, k.Get);
    // A function that reaches no dispatched call fits every bound.
    var l := bounded.certify(0, Leaf);
  }
}

// Stored bounded values: a constant field holds a call budget that a member spends later.
module Stored {
  trait {:termination false} {:termination_closed} T extends object {
    function f(): int
      ensures f() == 0
      decreases {:global} 1
  }
  function High(t: T): int
    ensures High(t) == 0
    decreases {:global} 2
  {
    t.f()
  }
}
module Observer {
  import Stored
  type Zero = h: bounded<nat, Stored.T -> int> | h.bound == 0
    ghost witness bounded.certify(0, (t: Stored.T) => 0)
  class C extends Stored.T {
    const cb: Zero
    constructor (h: Zero) { cb := h; }
    function f(): int
      ensures f() == 0
    {
      var ignored := cb(this);
      0
    }
  }
  method Main() {
    var h: Zero := bounded.certify(0, (t: Stored.T) => 0);
    var c := new C(h);
    var result := Stored.High(c);
    assert result == 0;
  }
}

// A mutable field, a sequence and a result hold bounded values; each application checks the bound of the value
// read in the current state, so replacing a value cannot close a loop.
module Mutable {
  class Iterator {
    var step: bounded<nat, int -> int>
    var pending: seq<bounded<nat, int -> int>>
    constructor (s: bounded<nat, int -> int>, rest: seq<bounded<nat, int -> int>>)
      ensures step == s && pending == rest
    {
      step := s;
      pending := rest;
    }
    method Next(x: int) returns (r: int)
      requires step.bound < 3 && forall i :: 0 <= i < |pending| ==> pending[i].bound < 3
      modifies this
      ensures step.bound < 3 && forall i :: 0 <= i < |pending| ==> pending[i].bound < 3
      decreases {:global} 3
    {
      r := step(x);
      if pending != [] {
        step := pending[0];
        pending := pending[1..];
      }
    }
  }
  function Make(k: nat): bounded<nat, int -> int>
    ensures Make(k).bound == k
  {
    bounded.certify(k, x => x + 1)
  }
  method Main() {
    var it := new Iterator(Make(2), [Make(1)]);
    it.pending := it.pending + [Make(0)];
    var a := it.Next(0);
    var b := it.Next(a);
  }
}
