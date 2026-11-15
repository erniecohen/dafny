// RUN: %exits-with 2 %verify "%s" > "%t"
// RUN: %exits-with 2 %verify --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 4 %verify --legacy-first-phase-const-reads "%S/Inputs/github-issue-339-legacy.dfy" >> "%t"
// RUN: %exits-with 4 %verify "%S/Inputs/github-issue-339-positive.dfy" >> "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-339-positive.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Regression (https://github.com/erniecohen/dafny/issues/339): before "new;", a constructor read a
// constant whose initializer depends on "this" as if every field the initializer reads already had
// its final value, so `t := One(s)` with `const s: D := t` proved false.  Such a read is now a
// resolution error, under both resolvers.  Constants whose initializers do not mention "this" may
// still be read before "new;", and every constant may be read after it
// (Inputs/github-issue-339-positive.dfy).  Under the hidden --legacy-first-phase-const-reads, the
// issue's program verifies again (Inputs/github-issue-339-legacy.dfy).

datatype D = Zero | One(D)

// The issue's program
class C {
  const t: D
  const s: D := t
  constructor () ensures false {
    t := One(s);  // error: s depends on this
    new;
    assert s == t;
    assert s < t;
  }
}

// The same through a seq field
class CSeq {
  const t: seq<D>
  const s: seq<D> := t
  constructor () {
    t := s + [Zero];  // error: s depends on this
  }
}

// The same through a function-valued constant, applied
class CArrow {
  const t: D
  const f: nat -> D := (n: nat) => t
  constructor () {
    t := One(f(0));  // error: f depends on this
  }
}

// An explicit "this", and a read outside the right-hand side of a field assignment
class CExplicit {
  const t: D
  const s: D := t
  constructor () {
    t := Zero;
    var x := this.s;  // error: s depends on this
  }
}

// Through another constant
class CChain {
  const t: D
  const s: D := t
  const u: D := One(s)
  constructor () {
    t := u;  // error: u depends on this (through s)
  }
}

// Through an instance function
class CFunction {
  const t: D
  const v: D := G()
  function G(): D { t }
  constructor () {
    t := One(v);  // error: v depends on this (it calls G)
  }
}

// An initializer that is "this" itself
class CSelf {
  const me: CSelf? := this
  var x: CSelf?
  constructor () {
    x := me;  // error: me depends on this
  }
}

// An inherited constant
trait T extends object {
  const t: D
  const s: D := t
}
class CInherited extends T {
  constructor () {
    t := One(s);  // error: s depends on this
  }
}
