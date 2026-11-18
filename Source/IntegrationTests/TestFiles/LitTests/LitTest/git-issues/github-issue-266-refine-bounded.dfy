// https://github.com/erniecohen/dafny/issues/266: refining a module that applies a bounded value.  The pre-type
// resolver resolved "c.f(x)", "c.f.requires(x)" and "c.f.reads(x)", for a bounded "c.f", by rewriting the syntax in
// place, and a refining module, which clones the resolved syntax of the module it refines, did not resolve: an
// assertion failed.  It now keeps the rewritten value apart from the syntax.  The legacy resolver, which resolves the
// rewritten syntax, is checked too.
// RUN: echo "the pre-type resolver" > "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh "%s" >> "%t"
// RUN: echo "the legacy resolver" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh=false "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

module A {
  class C {
    const f: bounded<nat, int -> int>
    constructor (f: bounded<nat, int -> int>) ensures this.f == f { this.f := f; }
  }

  function Apply(c: C, x: int): int
    requires c.f.requires(x)
    reads c, c.f.reads(x)
    decreases {:global} c.f.bound + 1
  {
    c.f(x)
  }

  method Run(c: C, x: int) returns (r: int)
    requires c.f.requires(x)
    decreases {:global} c.f.bound + 2
  {
    r := Apply(c, x);
  }
}

module B refines A {
  // Control: the refined module's bounded applications are verified in B
  method Control(c: C, x: int)
    requires c.f.requires(x)
    decreases {:global} c.f.bound + 1
  {
    var y := c.f(x);
    assert false; // error: control
  }

  // A bounded application at the bound of the bounded value
  function Flat(c: C, x: int): int
    requires c.f.requires(x)
    reads c, c.f.reads(x)
    decreases {:global} c.f.bound // error: the bound of c.f is not below the caller's
  {
    c.f(x)
  }
}
