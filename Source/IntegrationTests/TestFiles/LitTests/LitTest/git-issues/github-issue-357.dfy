// RUN: %exits-with 0 %baredafny build --target:lib --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --modular-termination "%s" --output "%t.doo" > "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --modular-termination --library "%t.doo" "%S/Inputs/github-issue-357-client.dfy" >> "%t"
// RUN: %exits-with 3 %baredafny build --target:lib --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --modular-termination "%S/Inputs/github-issue-357-user.dfy" --output "%t-user.doo" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Under --modular-termination, a library may refine a module with opaque functions
// (https://github.com/erniecohen/dafny/issues/357).  A refinement copies each member of the module
// it refines, among them the reveal lemmas Dafny generates for its opaque functions, which carry
// {:verify false}.  The copies are generated too, so the build no longer refuses them as unverified
// user members.  This is how Std.Strings refines Std.Arithmetic.LittleEndianNat, through an
// abstract module in between, as Base, Middle and Leaf do here.  A {:verify false} that the user
// wrote in a refining module is still refused (Inputs/github-issue-357-user.dfy).

abstract module Base {
  opaque function Double(n: nat): nat { n + n }

  opaque function Pick<T>(s: seq<T>, d: T): T { if |s| == 0 then d else s[0] }

  class Cell {
    var v: nat
    opaque function Get(): nat reads this { v }
  }

  lemma DoubleIs(n: nat)
    ensures Double(n) == 2 * n
  {
    reveal Double();
  }
}

abstract module Middle refines Base {
  lemma PickEmpty<T>(d: T)
    ensures Pick([], d) == d
  {
    reveal Pick();
  }
}

module Leaf refines Middle {
  lemma DoubleTwice(n: nat)
    ensures Double(Double(n)) == 4 * n
  {
    DoubleIs(n);
    DoubleIs(Double(n));
  }

  method UseCell(c: Cell)
    modifies c
    ensures c.Get() == 3
  {
    c.v := 3;
    reveal c.Get();
  }
}
