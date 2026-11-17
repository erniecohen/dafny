// RUN: echo "legacy resolver" > "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false "%s" >> "%t"
// RUN: echo "refreshed resolver" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --type-system-refresh --general-traits=datatype "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Regression (https://github.com/erniecohen/dafny/issues/485): a function that reads nothing, reading a
// constant whose initializer applies an arrow or calls a function that reads the heap, crashed the
// verifier with a NullReferenceException (in IllegalTriggerRemover).  Under both resolvers, each
// module is now verified, with one reachable assert false per module as a control.  A constant whose
// initializer reads the heap is not inlined into such a function; that initializer fails its own
// reads check (ReadsHeap).

module Total {
  class C {
    const cb: int -> int
    const v: int := cb(1)
    function get(): int { v }
    lemma Get() ensures get() == cb(1) {}
    lemma False() ensures false {}  // error: control
  }
  const f: int -> int := x => x + 1
  const w: int := f(2)
  function g(): int { w }
  lemma G() ensures g() == 3 {}
  lemma False() ensures false {}  // error: control
}

module Partial {
  class D {
    const cb: int --> int
    ghost const v: int := if cb.requires(1) then cb(1) else 0
    ghost function get(): int { v }
    lemma Get() requires cb.requires(1) ensures get() == cb(1) {}
    lemma False() ensures false {}  // error: control
  }
}

module ReadsHeap {
  class E {
    const cb: int ~> int
    ghost const r: bool := cb.requires(1)  // error: insufficient reads clause
    ghost function get(): bool { r }
    lemma False() ensures false {}  // error: control
  }
}
