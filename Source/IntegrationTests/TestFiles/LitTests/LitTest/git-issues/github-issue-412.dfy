// RUN: %exits-with 2 %resolve --allow-deprecation "%s" > "%t"
// RUN: %exits-with 2 %resolve --allow-deprecation --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %verify --allow-deprecation "%S/Inputs/github-issue-412-positive.dfy" >> "%t"
// RUN: %verify --allow-deprecation --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-412-positive.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Regression (https://github.com/erniecohen/dafny/issues/412): a statement added to an inherited method
// body by a skeleton could call a method that changes state the parent module knows about.  The
// inherited postcondition is assumed rather than proved again, so B.M below had both c.f == 1 and
// c.f == 2, and Bad proved false.  A direct assignment to such state was already rejected; a call to a
// method with a nonempty modifies clause is now rejected too, wherever it appears in the added
// statements.  Calls that cannot change state stay allowed (Inputs/github-issue-412-positive.dfy).

module A {
  class C {
    var f: int
    ghost var g: int
    method SetSelf() modifies this ensures f == 2 { f := 2; }
  }
  method Set2(c: C) modifies c ensures c.f == 2 { c.f := 2; }
  method Set2Ret(c: C) returns (r: int) modifies c ensures c.f == 2 { c.f := 2; r := 0; }
  ghost method GhostSet2(c: C) modifies c ensures c.g == 2 { c.g := 2; }
  method SetArr(a: array<int>) requires a.Length > 0 modifies a ensures a[0] == 2 { a[0] := 2; }
  datatype Outcome = Ok | Fail {
    predicate IsFailure() { this.Fail? }
    function PropagateFailure(): Outcome requires IsFailure() { Fail }
  }
  method Set2Outcome(c: C) returns (o: Outcome) modifies c ensures c.f == 2 && o == Ok { c.f := 2; o := Ok; }

  method M(c: C) modifies c ensures c.f == 1 { c.f := 1; }
  method MA(a: array<int>) requires a.Length > 0 modifies a ensures a[0] == 1 { a[0] := 1; }
  method MO(c: C) returns (o: Outcome) modifies c ensures c.f == 1 { c.f := 1; o := Ok; }
}

// The reproducer
module B1 refines A {
  method M ... ensures c.f == 2 { ...; Set2(c); }  // error
  method Bad(c: C) modifies c ensures false { M(c); }
}

// Inside an if
module B2 refines A {
  method M ... { ...; if c.f == 1 { Set2(c); } }  // error
}

// The result bound by var
module B3 refines A {
  method M ... { ...; var z := Set2Ret(c); }  // error
}

// A callee declared in the refining module
module B4 refines A {
  method SetB(c: C) modifies c ensures c.f == 2 { c.f := 2; }
  method M ... { ...; SetB(c); }  // error
}

// An array element
module B5 refines A {
  method MA ... { ...; SetArr(a); }  // error
}

// A method of the object
module B6 refines A {
  method M ... { ...; c.SetSelf(); }  // error
}

// :-
module B7 refines A {
  method MO ... { ...; :- Set2Outcome(c); }  // error
}

// Before the inherited body, and a ghost method
module B8 refines A {
  method M ... { Set2(c); GhostSet2(c); ...; }  // two errors
}

// Control: a direct assignment, rejected before this fix
module B9 refines A {
  method M ... { ...; c.f := 2; }  // error
}
