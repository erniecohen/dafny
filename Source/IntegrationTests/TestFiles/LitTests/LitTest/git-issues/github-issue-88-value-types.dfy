// RUN: %exits-with 4 %verify --general-traits=datatype "%s" > "%t"
// RUN: %exits-with 4 %verify --general-traits=datatype --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 4 %verify --general-traits=full "%S/Inputs/github-issue-88-value-types-newtypes.dfy" >> "%t"
// RUN: %exits-with 4 %verify --general-traits=full --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-88-value-types-newtypes.dfy" >> "%t"
// RUN: %exits-with 4 %verify --general-traits=full "%S/Inputs/github-issue-88-value-types-upcasts.dfy" >> "%t"
// RUN: %exits-with 4 %verify --inherited-trait-axioms "%S/Inputs/github-issue-88-value-types-axioms.dfy" >> "%t"
// RUN: %exits-with 4 %verify --inherited-trait-axioms --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-88-value-types-axioms.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --general-traits=full "%S/Inputs/github-issue-88-value-types-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --general-traits=full --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-88-value-types-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --general-traits=full "%S/Inputs/github-issue-88-value-types-rejected-later.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --general-traits=full --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-88-value-types-rejected-later.dfy" >> "%t"
// RUN: %exits-with 2 %resolve "%S/Inputs/github-issue-88-value-types-rejected-cardinality.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-88-value-types-rejected-cardinality.dfy" >> "%t"
// RUN: %exits-with 2 %resolve "%S/Inputs/github-issue-88-value-types-rejected-characteristics.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-88-value-types-rejected-characteristics.dfy" >> "%t"
// RUN: %exits-with 0 %baredafny build --target:lib --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh --general-traits=datatype "%S/Inputs/github-issue-88-value-types-library.dfy" --output "%t-lib.doo" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh --general-traits=datatype --library "%t-lib.doo" "%S/Inputs/github-issue-88-value-types-client.dfy" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh --general-traits=datatype --library "%t-lib.doo" "%S/Inputs/github-issue-88-value-types-client-rejected.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Issue 88 for value types.  A refining datatype, codatatype or newtype may add parent traits by
// writing an extends clause before the "...".  This needs no option, and the rules are the ones
// of a parent trait written on a fresh declaration.  As for classes and traits
// (github-issue-88.dfy), an added parent gives inherited text new obligations: an inherited
// member that becomes an override is checked against the member it overrides, and an inherited
// call that the override puts on a cycle must terminate.  Each rejected module is followed by a
// one-module control that is rejected for the same reason, and each accepted one ends in an
// assert false that fails.
//
// This file uses no conversion from a value type to a trait, which only the refreshed type system
// has.  Inputs/github-issue-88-value-types-upcasts.dfy has the programs that need one.  A newtype
// needs --general-traits=full to extend a trait, so the newtypes are in an input file as well.

// ----- A datatype and a codatatype gain a parent

module Issue0 {
  datatype D = X | Y {
    function One(): int
      ensures One() == 1
    {
      1
    }
  }
  codatatype Co = More(tail: Co) {
    function One(): int
      ensures One() == 1
    {
      1
    }
  }
}

module Issue1 refines Issue0 {
  trait S {
    const k: int := 5
    function One(): int
      ensures One() == 1
    function Two(): int {
      One() + 1
    }
  }
  datatype D extends S ... {}
  codatatype Co extends S ... {}

  method M(d: D, c: Co) {
    assert d.k == 5 && c.k == 5;
    assert d.Two() == 2 && c.Two() == 2;
    assert false; // error
  }
}

// ----- An inherited member that becomes an override is checked

module Override0 {
  datatype D = X {
    function F(): int { // error (in Override1): S.F promises 2
      1
    }
    lemma FIsOne()
      ensures F() == 1
    {
    }
  }
  codatatype Co = More(tail: Co) {
    function F(): int { // error (in Override1): S.F promises 2
      1
    }
  }
}

module Override1 refines Override0 {
  trait S {
    function F(): int
      ensures F() == 2
  }
  datatype D extends S ... {}
  codatatype Co extends S ... {}
}

module OverrideControl {
  trait S {
    function F(): int
      ensures F() == 2
  }
  datatype D extends S = X {
    function F(): int { // error: S.F promises 2
      1
    }
  }
}

// ----- A call in inherited text that the new override puts on a cycle must terminate

module Cycle0 {
  trait S {
    function F(s: S): int
  }
  datatype D = X {
    function F(s: S): int {
      s.F(s) + 1 // error (in Cycle1): s may be this
    }
    lemma Unfold(s: S)
      ensures F(s) == s.F(s) + 1
    {
    }
  }
}

module Cycle1 refines Cycle0 {
  datatype D extends S ... {}
}

module CycleControl {
  trait S {
    function F(s: S): int
  }
  datatype D extends S = X {
    function F(s: S): int {
      s.F(s) + 1 // error: s may be this
    }
  }
}

// ----- A chain of refinements, and an abstract import bound to its end

module Chain0 {
  datatype E = E0 {
    function One(): int
      ensures One() == 1
    {
      1
    }
    function Two(): int
      ensures Two() == 2
    {
      2
    }
  }
}

module Chain1 refines Chain0 {
  trait P {
    function One(): int
      ensures One() == 1
    function OneMore(): int {
      One() + 1
    }
  }
  datatype E extends P ... {}
}

module Chain2 refines Chain1 {
  trait Q {
    function Two(): int
      ensures Two() == 2
    function TwoMore(): int {
      Two() + 1
    }
  }
  datatype E extends Q ... {}

  method M(e: E) {
    assert e.OneMore() + e.TwoMore() == 5;
    assert false; // error
  }
}

abstract module Importer0 {
  import X : Chain0
  function F(e: X.E): int {
    e.One()
  }
}

module Importer1 refines Importer0 {
  import X = Chain2

  method M(e: X.E) {
    assert F(e) == 1 && e.TwoMore() == 3;
    assert false; // error
  }
}

// ----- A generic parent

module Generic0 {
  datatype G<X> = Mk(x: X) {
    function Get(): seq<X>
      ensures Get() == [x]
    {
      [x]
    }
  }
}

module Generic1 refines Generic0 {
  trait S<Y> {
    function Get(): seq<Y>
    function Len(): nat {
      |Get()|
    }
  }
  datatype G<X> extends S<X> ... {}

  method M(g: G<int>) {
    assert g.Len() == |g.Get()| == 1;
    assert false; // error
  }
}
