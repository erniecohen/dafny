// RUN: %exits-with 2 %resolve "%s" > "%t"
// RUN: %exits-with 2 %resolve --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 0 %verify "%S/Inputs/github-issue-483-accepted.dfy" >> "%t"
// RUN: %exits-with 0 %verify --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-483-accepted.dfy" >> "%t"
// RUN: %exits-with 0 %verify --legacy-function-store-locations "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Regression (https://github.com/erniecohen/dafny/issues/483): the check that rejects storing a
// function with read effects in memory looked only at assignments into a field or a one-dimensional
// array element.  A store into a multi-dimensional array element, or the result of a method call
// stored into a field or an array element, could tie a knot and prove false.  Each method below
// proved false; the legacy option restores that.

datatype Pack<T> = Pack(ghost c: T)

method Multi() ensures false {
  var r := new Pack<() ~> bool>[1, 1];
  var tf := Pack(() reads * =>
                   if r.Length0 > 0 && r.Length1 > 0 && r[0, 0].c.requires() then !r[0, 0].c() else false);
  r[0, 0] := tf; // error
  assert r[0, 0].c.requires();
  assert r[0, 0].c() == !r[0, 0].c();
}

method Id(p: Pack<() ~> bool>) returns (q: Pack<() ~> bool>) ensures q == p { q := p; }

method CallIntoArray() ensures false {
  var r := new Pack<() ~> bool>[1];
  var tf := Pack(() reads * => if r.Length > 0 && r[0].c.requires() then !r[0].c() else false);
  r[0] := Id(tf); // error
  assert r[0].c.requires();
  assert r[0].c() == !r[0].c();
}

method CallIntoMulti() ensures false {
  var r := new Pack<() ~> bool>[1, 1];
  var tf := Pack(() reads * =>
                   if r.Length0 > 0 && r.Length1 > 0 && r[0, 0].c.requires() then !r[0, 0].c() else false);
  r[0, 0] := Id(tf); // error
  assert r[0, 0].c.requires();
  assert r[0, 0].c() == !r[0, 0].c();
}

class Ref {
  ghost var w: () ~> bool
  constructor () { w := () => true; }
  method Set(ghost f: () ~> bool) modifies this ensures w == f {
    w := Ghost(f); // error: an implicit this.w
  }
}

method Ghost(ghost f: () ~> bool) returns (ghost g: () ~> bool) ensures g == f { g := f; }

method CallIntoField() ensures false {
  var r := new Ref();
  ghost var knot := () reads * => if r.w.requires() then !r.w() else false;
  r.w := Ghost(knot); // error
  assert knot.requires();
  assert knot() == !knot();
}

method Two(p: Pack<() ~> bool>) returns (x: int, q: Pack<() ~> bool>) ensures q == p { x, q := 0, p; }

method CallSecondResult() ensures false {
  var r := new Pack<() ~> bool>[1];
  var x;
  var tf := Pack(() reads * => if r.Length > 0 && r[0].c.requires() then !r[0].c() else false);
  x, r[0] := Two(tf); // error
  assert r[0].c.requires();
  assert r[0].c() == !r[0].c();
}

// A yield parameter is a field of the iterator
iterator It(ghost f: () ~> bool) yields (ghost y: () ~> bool) {
  y := Ghost(f); // error
  yield;
}
