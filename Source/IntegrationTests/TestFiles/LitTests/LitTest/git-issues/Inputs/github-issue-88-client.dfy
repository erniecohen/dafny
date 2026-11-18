// A client of github-issue-88-library.dfy.

// Refining a library module and adding a parent to one of its traits.

module Mine refines Lib0 {
  trait Even extends object {
    function Size(): nat ensures Size() % 2 == 0
  }
  trait T ... extends Even {}
  method Use() {
    var c := new C();
    var e: Even := c;
    assert e.Size() % 2 == 0;
  }
  method UseControl(e: Even) {
    assert e.Size() % 2 == 0;
    assert e.Size() == 2; // error
  }
}

// The override check of an inherited library member against the added parent is made here.

module Unchecked refines LibWeak {
  trait Even extends object {
    function Size(): nat ensures Size() % 2 == 0
  }
  trait T ... extends Even {} // error (reported at LibWeak.T.Size, which has no such postcondition)
}

// Adding a library trait as a parent.  The parent is in another module, so the rule for
// cross-module parents applies to the refining declaration.

module App0 {
  trait T extends object {
    function Name(): string { "app" }
  }
  class C extends T {
    constructor () {}
  }
}
module App1 refines App0 {
  import LibTraits
  @AssumeCrossModuleTermination
  trait T ... extends LibTraits.Named {}
  @AssumeCrossModuleTermination
  class C ... {}
  method Use() {
    var c := new C();
    var n: LibTraits.Named := c;
    assert n.Length() == 3;
  }
  method UseControl(n: LibTraits.Named) {
    assert n.Length() == |n.Name()|;
    assert n.Length() == 3; // error
  }
}

// The library's own refinement is usable.

module UsesLib1 {
  import Lib1
  method Use() {
    var c := new Lib1.C();
    var s: Lib1.Sized := c;
    assert s.Size() == 2;
    assert false; // error
  }
}
