// RUN: %exits-with 4 %verify --refinement-extends "%s" > "%t"
// RUN: %exits-with 4 %verify --refinement-extends --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 2 %verify "%s" >> "%t"
// RUN: %exits-with 2 %verify --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 2 %resolve --refinement-extends "%S/Inputs/github-issue-88-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --refinement-extends --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-88-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --refinement-extends "%S/Inputs/github-issue-88-rejected-later.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --refinement-extends --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-88-rejected-later.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --refinement-extends "%S/Inputs/github-issue-88-rejected-characteristics.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --refinement-extends --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-88-rejected-characteristics.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --refinement-extends "%S/Inputs/github-issue-88-rejected-cardinality.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --refinement-extends --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-88-rejected-cardinality.dfy" >> "%t"
// RUN: %exits-with 0 %baredafny build --target:lib --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --refinement-extends "%S/Inputs/github-issue-88-library.dfy" --output "%t-lib.doo" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --refinement-extends --library "%t-lib.doo" "%S/Inputs/github-issue-88-client.dfy" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --refinement-extends --type-system-refresh=false --general-newtypes=false --library "%t-lib.doo" "%S/Inputs/github-issue-88-client.dfy" >> "%t"
// RUN: %exits-with 2 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --refinement-extends --library "%t-lib.doo" "%S/Inputs/github-issue-88-client-rejected.dfy" >> "%t"
// RUN: %exits-with 1 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --library "%t-lib.doo" "%S/Inputs/github-issue-88-plain-client.dfy" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --refinement-extends --library "%t-lib.doo" "%S/Inputs/github-issue-88-plain-client.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Feature (https://github.com/erniecohen/dafny/issues/88): under --refinement-extends, a class or trait
// declaration in a refining module may add parent traits to the declaration it refines:
// "trait T ... extends S { }".  The parents of the refined declaration are kept and the added ones
// follow them.  Without the option the syntax is a parse error, as before (the third and fourth RUN
// lines).  The Inputs files hold the programs that resolution rejects, and a library with its clients.
//
// An added parent creates obligations in the refining module, also at text the module inherits:
// override checks, definite assignment of the parent's fields, and termination of calls that the new
// overrides put on a cycle.  Each of those is tested here with a program that would prove false if
// the obligation were assumed.  Each accepted use has a control that ends in "assert false" and
// must fail.

// ----- The issue's program

module IssueA {
  trait T {}
}
module IssueB refines IssueA {
  trait S {}
  trait T ... extends S {}
  method Upcast(t: T) returns (s: S) {
    s := t;
  }
  method UpcastControl(t: T) returns (s: S) {
    s := t;
    assert false; // error
  }
}

// ----- A value of the refined type is a value of the added parent

module Value0 {
  trait T extends object {
    function Value(): int
  }
  class C extends T {
    constructor () {}
    function Value(): int { 3 }
  }
}
module Value1 refines Value0 {
  trait S extends object {
    function Value(): int
    function Twice(): int { 2 * Value() }
  }
  trait T ... extends S {}
  method Use() {
    var c := new C();
    var s: S := c;
    assert s.Twice() == 6;
    var t: T := c;
    assert t.Twice() == 6;
  }
  method UseControl() {
    var c := new C();
    var s: S := c;
    assert s.Twice() == 6;
    assert false; // error
  }
}

// ----- A class may add a parent too

module Class0 {
  class C {
    var x: int
    constructor () ensures x == 0 { x := 0; }
    function Get(): int reads this { x }
  }
}
module Class1 refines Class0 {
  trait Getter extends object {
    function Get(): int reads this
  }
  class C ... extends Getter {}
  method Use(g: Getter) returns (r: int) {
    r := g.Get();
  }
  method Main() {
    var c := new C();
    var r := Use(c);
    assert c.Get() == 0;
  }
  method MainControl() {
    var c := new C();
    var g: Getter := c;
    assert g.Get() == 0;
    assert false; // error
  }
}

// ----- An inherited member that becomes an override is checked against the added parent

module Override0 {
  trait T extends object {
    function F(): int { 0 } // error: in Override1, T.F does not meet the specification of S.F
    lemma L() ensures true {} // error: in Override1, T.L does not meet the specification of S.L
  }
}
module Override1 refines Override0 {
  trait S extends object {
    function F(): int ensures F() > 0
    lemma L() ensures false
  }
  trait T ... extends S {}
}

// The same for a member of a class below the refined trait.

module OverrideBelow0 {
  trait T extends object {}
  class C extends T {
    constructor () {}
    lemma L() ensures true {} // error: in OverrideBelow1, C.L does not meet the specification of S.L
    function F(): int { 0 } // error: in OverrideBelow1, C.F does not meet the specification of S.F
  }
}
module OverrideBelow1 refines OverrideBelow0 {
  trait S extends object {
    function F(): int ensures F() > 0
    lemma L() ensures false
  }
  trait T ... extends S {}
  method WouldBeBad() {
    var c := new C();
    var s: S := c;
    s.L();
    assert false;
  }
}

// An inherited member that meets the added parent's specification is accepted.

module OverrideGood0 {
  trait T extends object {
    function F(): int ensures F() > 0 { 1 }
    method M() returns (r: int) ensures r == 7 { r := 7; }
  }
  class C extends T {
    constructor () {}
  }
}
module OverrideGood1 refines OverrideGood0 {
  trait S extends object {
    function F(): int ensures F() > 0
    method M() returns (r: int) ensures r > 0 { r := 1; }
  }
  trait T ... extends S {}
  method Use() {
    var c := new C();
    var s: S := c;
    var r := s.M();
    assert r > 0 && s.F() > 0;
  }
  method UseControl() {
    var c := new C();
    var s: S := c;
    var r := s.M();
    assert r == 7; // error: only the specification of S.M is known
  }
  method UseControl2() {
    var c := new C();
    var s: S := c;
    assert s.F() == 1;
    assert false; // error
  }
}

// ----- A field of the added parent must be assigned by inherited constructors

module Field0 {
  trait T extends object {}
  class C extends T {
    constructor () {} // error: field 'e' of the parent added in Field1 is not assigned
  }
}
module Field1 refines Field0 {
  type Empty = x: int | false witness *
  trait S extends object {
    var e: Empty
  }
  trait T ... extends S {}
  method WouldBeBad() {
    var c := new C();
    var x: Empty := c.e;
    assert false;
  }
}

// A field of an auto-initializable type needs no assignment.

module FieldGood0 {
  trait T extends object {}
  class D extends T {
    constructor () {}
  }
}
module FieldGood1 refines FieldGood0 {
  trait S extends object {
    var n: nat
  }
  trait T ... extends S {}
  method Use() {
    var d := new D();
    assert d.n >= 0;
  }
  method UseControl() {
    var d := new D();
    assert d.n == 0; // error
  }
}

// ----- A call in inherited text that the new override puts on a cycle must terminate

module Cycle0 {
  trait S extends object {
    function F(s: S): int
  }
  trait T extends object {
    function F(s: S): int { s.F(s) + 1 } // error: in Cycle1, s may be this
    lemma Unfold(s: S) ensures F(s) == s.F(s) + 1 {}
  }
  class C extends T {
    constructor () {}
  }
}
module Cycle1 refines Cycle0 {
  trait T ... extends S {}
  method WouldBeBad() {
    var c := new C();
    var s: S := c;
    c.Unfold(s);
    assert s.F(s) == c.F(s);
    assert false;
  }
}

// ----- Diamond: the refined trait and the added parent share an ancestor

module Diamond0 {
  trait P extends object {
    function Id(): int
    lemma IdIsPositive() ensures Id() > 0
  }
  trait T extends P {}
  class C extends T {
    constructor () {}
    function Id(): int { 5 }
    lemma IdIsPositive() ensures Id() > 0 {}
  }
}
module Diamond1 refines Diamond0 {
  trait S extends P {
    function Next(): int { Id() + 1 }
  }
  trait T ... extends S {}
  method Use() {
    var c := new C();
    var s: S := c;
    s.IdIsPositive();
    assert s.Next() > 1;
    assert s.Next() == 6;
  }
  method UseControl(s: S) {
    s.IdIsPositive();
    assert s.Next() > 1;
    assert s.Next() == 6; // error
  }
}

// ----- A chain of refinements, each adding a parent, and an abstract import bound to the last

module Chain0 {
  trait T extends object {}
  class C extends T {
    constructor () {}
  }
}
module Chain1 refines Chain0 {
  trait S extends object {
    function One(): int ensures One() == 1
  }
  trait T ... extends S {}
  class C ... {
    function One(): int ensures One() == 1 { 1 }
  }
}
module Chain2 refines Chain1 {
  trait R extends object {
    function Two(): int ensures Two() == 2
  }
  trait T ... extends R {}
  class C ... {
    function Two(): int ensures Two() == 2 { 2 }
  }
  method Use() {
    var c := new C();
    var s: S := c;
    var r: R := c;
    assert s.One() + r.Two() == 3;
  }
  method UseControl() {
    var c := new C();
    var s: S := c;
    var r: R := c;
    assert s.One() + r.Two() == 3;
    assert false; // error
  }
}
abstract module Client0 {
  import X : Chain0
  method Make() returns (t: X.T) {
    t := new X.C();
  }
}
module Client2 refines Client0 {
  import X = Chain2
  method Use() {
    var t := Make();
    var r: X.R := t;
    assert r.Two() == 2;
  }
  method UseControl() {
    var t := Make();
    var s: X.S := t;
    assert s.One() == 1;
    assert false; // error
  }
}

// A class of the importing module below the imported trait gets the override check of the parent
// that the bound module added.

module Bound0 {
  trait T extends object {}
}
module Bound1 refines Bound0 {
  trait S extends object {
    lemma L() ensures false
  }
  trait T ... extends S {}
}
abstract module Importer0 {
  import X : Bound0
  @AssumeCrossModuleTermination
  class D extends X.T {
    constructor () {}
    lemma L() ensures true {} // error: in Importer1, D.L does not meet the specification of X.S.L
  }
}
module Importer1 refines Importer0 {
  import X = Bound1
  method WouldBeBad() {
    var d := new D();
    var s: X.S := d;
    s.L();
    assert false;
  }
}

// ----- Generic: the added parent's type arguments use the refined trait's type parameters

module Generic0 {
  trait T<X> extends object {
    function Get(): X
  }
  class C extends T<int> {
    constructor () {}
    function Get(): int { 4 }
    function Wrapped(): seq<int> { [Get()] }
  }
}
module Generic1 refines Generic0 {
  trait S<Y> extends object {
    function Wrapped(): Y
  }
  trait T<X> ... extends S<seq<X>> {}
  method Use() {
    var c := new C();
    var s: S<seq<int>> := c;
    assert s.Wrapped() == [4];
  }
  method UseControl(t: T<bool>) {
    var s: S<seq<bool>> := t;
    assert |s.Wrapped()| == 1; // error
  }
}
