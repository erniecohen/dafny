// RUN: %verify --copy-imports "%s" > "%t"
// RUN: %verify --copy-imports --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 2 %verify --copy-imports "%S/Inputs/git-issue-107-rebind-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %verify --copy-imports --type-system-refresh=false --general-newtypes=false "%S/Inputs/git-issue-107-rebind-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %verify --copy-imports "%S/Inputs/git-issue-107-rebind-capture.dfy" >> "%t"
// RUN: %exits-with 2 %verify --copy-imports --type-system-refresh=false --general-newtypes=false "%S/Inputs/git-issue-107-rebind-capture.dfy" >> "%t"
// RUN: %exits-with 4 %verify --copy-imports "%S/Inputs/git-issue-107-rebind-controls.dfy" >> "%t"
// RUN: %exits-with 4 %verify --copy-imports --type-system-refresh=false --general-newtypes=false "%S/Inputs/git-issue-107-rebind-controls.dfy" >> "%t"
// RUN: %exits-with 4 %verify --copy-imports --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%S/Inputs/git-issue-107-rebind-invariants.dfy" >> "%t"
// RUN: %exits-with 2 %verify "%s" >> "%t"
// RUN: %run --copy-imports --target cs "%S/Inputs/git-issue-107-rebind-run.dfy" >> "%t"
// RUN: %run --copy-imports --target cs --type-system-refresh=false --general-newtypes=false "%S/Inputs/git-issue-107-rebind-run.dfy" >> "%t"
// RUN: %run --copy-imports --target py "%S/Inputs/git-issue-107-rebind-run.dfy" >> "%t"
// RUN: %build -t:lib --copy-imports "%S/Inputs/git-issue-107-rebind-library.dfy" --output "%t.library.doo" >> "%t"
// RUN: %verify --copy-imports "%S/Inputs/git-issue-107-rebind-library-user.dfy" --library "%t.library.doo" >> "%t"
// RUN: %verify --copy-imports --type-system-refresh=false --general-newtypes=false "%S/Inputs/git-issue-107-rebind-library-user.dfy" --library "%t.library.doo" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Rebinding an inherited copy keeps its identity fresh and may implement abstract declarations.
// https://github.com/erniecohen/dafny/issues/107

abstract module Keys {
  type T
  function Next(t: T): T
}
abstract module Keys1 refines Keys { }
module IntKeys refines Keys1 {
  type T = int
  function Next(t: T): T { t + 1 }
}
abstract module Client {
  import X := Keys
  function Twice(t: X.T): X.T { X.Next(X.Next(t)) }
  lemma Same(t: X.T) ensures Twice(t) == X.Next(X.Next(t)) { }
}
abstract module Step refines Client {
  import X := Keys1
}
module Final refines Step {
  import X := IntKeys
  lemma Use() ensures Twice(0) == 2 { Same(0); }
}
module FinalAgain refines Final {
  import X := IntKeys
  lemma UseAgain() ensures Twice(0) == 2 { Use(); }
}
module Cells { class C { constructor () { } } }
module Cells1 refines Cells { }
module M {
  import X := Cells
  import Z := Cells
  lemma Distinct(o: object) ensures o is X.C ==> !(o is Z.C) { }
}
module M1 refines M {
  import X := Cells1
  import M
  import Cells1
  lemma Fresh(o: object)
    ensures o is X.C ==> !(o is Cells1.C)
    ensures o is X.C ==> !(o is M.X.C)
  { }
}
module Middle { import Y := Cells }
module Middle1 refines Middle { import Y := Cells1 }
module Outer { import X := Middle }
module Outer1 refines Outer { import X := Middle1 }

// Opened copies keep the inherited unqualified occurrences bound to their counterparts.
module Values { function Value(): int { 1 } }
module Values1 refines Values { }
module Opened {
  import opened X := Values
  function F(): int { Value() }
}
module Opened1 refines Opened {
  import opened X := Values1
  lemma Use() ensures F() == 1 { }
}

// A larger selection of named export sets is accepted.
module Exports {
  export E reveals Value
  export More extends E reveals Extra
  function Value(): int { 1 }
  function Extra(): int { 2 }
}
module Exports1 refines Exports { }
module ExportUser {
  import X := Exports`E
  function F(): int { X.Value() }
}
module ExportUser1 refines ExportUser {
  import X := Exports1`{E, More}
  lemma Use() ensures F() + X.Extra() == 3 { }
}

// An abstract import inside the copied module is implemented by a further refinement.
abstract module Dependency { import A : Keys }
module Dependency1 refines Dependency { import A := IntKeys }
abstract module HasDependency { import X := Dependency }
module HasDependency1 refines HasDependency {
  import X := Dependency1
  lemma Use() ensures X.A.Next(0) == 1 { }
}

// Generic declarations of a rebound copy are still its own declarations.
module Generics {
  datatype List<T> = Nil | Cons(head: T, tail: List<T>)
  class Box<T> {
    const value: T
    constructor (v: T) ensures value == v { value := v; }
  }
}
module Generics1 refines Generics { }
module GenericUser {
  import X := Generics
  function Head(l: X.List<int>): int { if l.Nil? then 0 else l.head }
  method Make() returns (b: X.Box<int>) ensures b.value == 3 {
    b := new X.Box<int>(3);
  }
}
module GenericUser1 refines GenericUser {
  import X := Generics1
  method Use() {
    assert Head(X.Cons(4, X.Nil)) == 4;
    var b := Make();
    assert b.value == 3;
  }
}
