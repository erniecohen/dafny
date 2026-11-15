// https://github.com/erniecohen/dafny/issues/266, milestone 1
// Under --modular-termination, the system module declares the type bounded<O, F> of a bounded callable: a function
// value F with an order O, nat or ORDINAL.  The type has the subtyping of F, no equality, no default value and no
// nonemptiness, and the compilers erase it to F.  Milestone 1 has no way to form, apply or project a value.
// RUN: echo "positive, both resolvers" > "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --modular-termination "%s" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --modular-termination --type-system-refresh --general-traits=datatype "%s" >> "%t"
// RUN: echo "false controls: one reachable assert false per family, both resolvers" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --modular-termination "%S/Inputs/github-issue-266-false.dfy" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --modular-termination --type-system-refresh --general-traits=datatype "%S/Inputs/github-issue-266-false.dfy" >> "%t"
// RUN: echo "conversions that are not subtypes, both resolvers" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --modular-termination "%S/Inputs/github-issue-266-verify.dfy" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --modular-termination --type-system-refresh --general-traits=datatype "%S/Inputs/github-issue-266-verify.dfy" >> "%t"
// RUN: echo "no default value, both resolvers" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --modular-termination "%S/Inputs/github-issue-266-init.dfy" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --modular-termination --type-system-refresh --general-traits=datatype "%S/Inputs/github-issue-266-init.dfy" >> "%t"
// RUN: echo "resolution errors, both resolvers" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --modular-termination "%S/Inputs/github-issue-266-resolve.dfy" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --modular-termination --type-system-refresh --general-traits=datatype "%S/Inputs/github-issue-266-resolve.dfy" >> "%t"
// RUN: echo "formation and characteristic errors, both resolvers" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --modular-termination "%S/Inputs/github-issue-266-chars.dfy" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --modular-termination --type-system-refresh --general-traits=datatype "%S/Inputs/github-issue-266-chars.dfy" >> "%t"
// RUN: echo "without --modular-termination, both resolvers" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false "%S/Inputs/github-issue-266-off.dfy" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --type-system-refresh --general-traits=datatype "%S/Inputs/github-issue-266-off.dfy" >> "%t"
// RUN: echo "erasure" >> "%t"
// RUN: %exits-with 0 %baredafny run --no-verify --use-basename-for-filename --modular-termination --target cs "%S/Inputs/github-issue-266-erase.dfy" >> "%t"
// RUN: %exits-with 0 %baredafny translate cs --no-verify --use-basename-for-filename --modular-termination --output "%t.erase.cs" "%S/Inputs/github-issue-266-erase.dfy" >> "%t"
// RUN: %OutputCheck --file-to-check "%t.erase.cs" "%S/Inputs/github-issue-266-erase.dfy"
// RUN: %diff "%s.expect" "%t"

// Positive: bounded types in parameters, results, locals, fields, constants, datatypes, sequences, maps and
// generic code; conversions along the payload's arrow subtyping; ghost equality; and a user declaration of
// "bounded" that hides the system type.
module Flows {
  datatype Box<T> = Box(t: T)
  class Holder {
    var f: bounded<nat, int -> int>
    const g: bounded<nat, int --> int>
    constructor (f: bounded<nat, int -> int>, g: bounded<nat, int --> int>) { this.f := f; this.g := g; }
  }
  class Cell<T> { var t: T  constructor (t: T) { this.t := t; } }
  function Id<T>(x: T): T { x }
  type Order = nat
  type Payload = int -> int

  // A bounded type has the subtyping of its payload: int -> nat is a subtype of int -> int, which is a
  // subtype of int --> int and of int ~> int.
  method Pass(c: bounded<nat, int -> nat>, d: bounded<Order, Payload>, o: bounded<ORDINAL, int ~> int>)
    returns (r: bounded<nat, int --> int>, s: seq<bounded<nat, int -> int>>, b: Box<bounded<nat, int -> int>>,
             p: bounded<ORDINAL, int ~> int>)
    ensures |s| == 2 && s[1] == c && b.t == Id(d) && p == o
  {
    r := c;
    s := [d, c];
    b := Box(Id(d));
    p := o;
    var h := new Holder(d, c);
    h.f := c;
    assert h.f == c;
    var e := new Cell(r);
    var m: map<int, bounded<nat, int -> int>> := map[0 := d, 1 := c];
    assert m[1] == c;
  }

  // Ghost equality and ghost sets.
  ghost function Pair(c: bounded<nat, int -> int>, d: bounded<nat, int -> int>): set<bounded<nat, int -> int>> {
    {c, d}
  }
  lemma Members(c: bounded<nat, int -> int>, d: bounded<nat, int -> int>)
    ensures c in Pair(c, d) && (c == d ==> |Pair(c, d)| == 1)
  {
  }
}

// A module's own declaration of "bounded" hides the system type.
module Shadow {
  type bounded<A, B> = int
  method M(x: bounded<int, int>) returns (y: int) ensures y == x + 1 { y := x + 1; }
}
module ShadowDatatype {
  datatype bounded = B
  method M() returns (x: bounded) ensures x == B { x := B; }
}

// So does an opened import's; a qualified import does not.
module Lib { type bounded = int }
module Opened {
  import opened Lib
  method M(x: bounded) returns (y: int) ensures y == x { y := x; }
}
module Qualified {
  import Lib
  method M(x: Lib.bounded, c: bounded<nat, int -> int>) returns (y: int, d: bounded<nat, int -> int>)
    ensures y == x && d == c
  {
    y := x;
    d := c;
  }
}
