// RUN: %verify --solver-path %review-z3 "%s" > "%t"
// RUN: %verify --solver-path %review-z3 --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 4 %verify --solver-path %review-z3 "%S/Inputs/git-issue-266-persistence-false.dfy" >> "%t"
// RUN: %exits-with 4 %verify --solver-path %review-z3 --type-system-refresh=false --general-newtypes=false "%S/Inputs/git-issue-266-persistence-false.dfy" >> "%t"
// RUN: %exits-with 4 %verify --solver-path %review-z3 "%S/Inputs/git-issue-266-persistence-knots.dfy" >> "%t"
// RUN: %exits-with 4 %verify --solver-path %review-z3 --type-system-refresh=false --general-newtypes=false "%S/Inputs/git-issue-266-persistence-knots.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// https://github.com/erniecohen/dafny/issues/266: storage and return preserve the
// selected package, including its bound. No migration flag or trust list is needed.

module Persistence {
  datatype Wrapped = Wrapped(callback: bounded<nat, int -> int>)

  class Holder {
    const wrapped: Wrapped
    ghost const proof: bounded<nat, int -> int>

    constructor (f: bounded<nat, int -> int>, ghost g: bounded<nat, int -> int>)
      ensures wrapped == Wrapped(f) && proof == g
    {
      wrapped := Wrapped(f);
      proof := g;
    }

    function Return(): bounded<nat, int -> int> { wrapped.callback }
    ghost function ReturnGhost(): bounded<nat, int -> int> { proof }
  }

  function Identity<T>(x: T): T { x }

  method Positive()
    decreases {:global} 1
  {
    var f := bounded.certify(0, (x: int) => x + 1);
    ghost var g := bounded.certify(0, (x: int) => x + 2);
    var holder := new Holder(Identity(f), Identity(g));
    var returned := holder.Return();
    ghost var returnedGhost := holder.ReturnGhost();
    assert returned.bound == 0 && returnedGhost.bound == 0;
    assert returned(20) == 21 && returnedGhost(20) == 22;
    // Duplicating or returning a package does not consume its evidence.
    assert holder.Return()(21) == 22 && holder.ReturnGhost()(21) == 23;
  }
}
