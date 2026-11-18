// RUN: %exits-with 2 %verify "%s" > "%t"
// RUN: %exits-with 2 %resolve --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 0 %verify --allow-warnings --legacy-unchecked-type-variance "%s" >> "%t"
// RUN: %exits-with 0 %verify --allow-warnings --legacy-unchecked-type-variance --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 2 %verify "%S/Inputs/git-issue-491-opaque.dfy" >> "%t"
// RUN: %exits-with 0 %verify --legacy-unchecked-type-variance "%S/Inputs/git-issue-491-opaque.dfy" >> "%t"
// RUN: %exits-with 2 %verify "%S/Inputs/git-issue-491-type-test.dfy" >> "%t"
// RUN: %exits-with 0 %verify --legacy-unchecked-type-variance "%S/Inputs/git-issue-491-type-test.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Constraint variance: https://github.com/erniecohen/dafny/issues/491.
// Each module contains a reachable false proof under the legacy rule.

module Forall {
  type Empty = x: int | false witness *
  ghost predicate F<X(!new)>(y: X) { false }
  ghost predicate Some<X(!new)>() { exists y: X {:nowarn} :: true }
  datatype Box<+X(!new)> = Box(b: bool)
  type S<+X(!new)> = s: Box<X> | s.b ==> (forall y: X :: F(y)) witness *
  lemma Contradiction(t: S<int>) requires t.b ensures false {
  assert F(0);
  assert false;
  }
  lemma FalseProof() {
  var s: S<Empty> := Box(true);
  var t: S<int> := s;
  Contradiction(t);
  assert false;
  }
}

module Not {
  type Empty = x: int | false witness *
  ghost predicate F<X(!new)>(y: X) { false }
  ghost predicate Some<X(!new)>() { exists y: X {:nowarn} :: true }
  datatype Box<+X(!new)> = Box(b: bool)
  type S<+X(!new)> = s: Box<X> | s.b ==> (!Some<X>()) witness *
  lemma Contradiction(t: S<int>) requires t.b ensures false {
  assert 0 is nat;
  assert Some<int>();
  assert false;
  }
  lemma FalseProof() {
  var s: S<Empty> := Box(true);
  var t: S<int> := s;
  Contradiction(t);
  assert false;
  }
}

module Implication {
  type Empty = x: int | false witness *
  ghost predicate F<X(!new)>(y: X) { false }
  ghost predicate Some<X(!new)>() { exists y: X {:nowarn} :: true }
  datatype Box<+X(!new)> = Box(b: bool)
  type S<+X(!new)> = s: Box<X> | s.b ==> (Some<X>() ==> false) witness *
  lemma Contradiction(t: S<int>) requires t.b ensures false {
  assert 0 is nat;
  assert Some<int>();
  assert false;
  }
  lemma FalseProof() {
  var s: S<Empty> := Box(true);
  var t: S<int> := s;
  Contradiction(t);
  assert false;
  }
}

module Equality {
  type Empty = x: int | false witness *
  ghost predicate F<X(!new)>(y: X) { false }
  ghost predicate Some<X(!new)>() { exists y: X {:nowarn} :: true }
  datatype Box<+X(!new)> = Box(b: bool)
  type S<+X(!new)> = s: Box<X> | s.b ==> (Some<X>() == false) witness *
  lemma Contradiction(t: S<int>) requires t.b ensures false {
  assert 0 is nat;
  assert Some<int>();
  assert false;
  }
  lemma FalseProof() {
  var s: S<Empty> := Box(true);
  var t: S<int> := s;
  Contradiction(t);
  assert false;
  }
}

module Equivalence {
  type Empty = x: int | false witness *
  ghost predicate F<X(!new)>(y: X) { false }
  ghost predicate Some<X(!new)>() { exists y: X {:nowarn} :: true }
  datatype Box<+X(!new)> = Box(b: bool)
  type S<+X(!new)> = s: Box<X> | s.b ==> (Some<X>() <==> false) witness *
  lemma Contradiction(t: S<int>) requires t.b ensures false {
  assert 0 is nat;
  assert Some<int>();
  assert false;
  }
  lemma FalseProof() {
  var s: S<Empty> := Box(true);
  var t: S<int> := s;
  Contradiction(t);
  assert false;
  }
}

module Conditional {
  type Empty = x: int | false witness *
  ghost predicate F<X(!new)>(y: X) { false }
  ghost predicate Some<X(!new)>() { exists y: X {:nowarn} :: true }
  datatype Box<+X(!new)> = Box(b: bool)
  type S<+X(!new)> = s: Box<X> | s.b ==> (if Some<X>() then false else true) witness *
  lemma Contradiction(t: S<int>) requires t.b ensures false {
  assert 0 is nat;
  assert Some<int>();
  assert false;
  }
  lemma FalseProof() {
  var s: S<Empty> := Box(true);
  var t: S<int> := s;
  Contradiction(t);
  assert false;
  }
}

module Covariant {
  type Empty = x: int | false witness *
  ghost predicate F<X(!new)>(y: X) { false }
  ghost predicate Some<X(!new)>() { exists y: X {:nowarn} :: true }
  datatype Box<+X(!new)> = Box(b: bool)
  type S<+X(!new)> = s: Box<X> | s.b ==> (forall y: seq<X> :: |y| == 0) witness *
  lemma Contradiction(t: S<int>) requires t.b ensures false {
  assert [0] == [0];
  assert false;
  }
  lemma FalseProof() {
  forall q: seq<Empty> ensures |q| == 0 {
    if |q| > 0 { var e := q[0]; assert false; }
  }
  var s: S<Empty> := Box(true);
  var t: S<int> := s;
  Contradiction(t);
  assert false;
  }
}

module Generic {
  type Empty = x: int | false witness *
  ghost predicate F<X(!new)>(y: X) { false }
  ghost predicate Some<X(!new)>() { exists y: X {:nowarn} :: true }
  datatype Box<+X(!new)> = Box(b: bool)
  ghost predicate All<X(!new)>(n: nat) { (forall y: X :: F(y)) && (n == 0 || All<X>(n-1)) }
  type S<+X(!new)> = s: Box<X> | s.b ==> (All<X>(1)) witness *
  lemma Contradiction(t: S<int>) requires t.b ensures false {
  assert F(0);
  assert false;
  }
  lemma FalseProof() {
  var s: S<Empty> := Box(true);
  var t: S<int> := s;
  Contradiction(t);
  assert false;
  }
}

module Contravariant {
  type Empty = x: int | false witness *
  ghost predicate F<X(!new)>(y: X) { false }
  ghost predicate Some<X(!new)>() { exists y: X {:nowarn} :: true }
  datatype Box<+X(!new)> = Box(b: bool)
  type S<+X(!new)> = s: Box<X> | s.b ==> (exists f: X -> Empty {:nowarn} :: true) witness *
  lemma Contradiction(t: S<int>) requires t.b ensures false {
  var f: int -> Empty :| true;
  var impossible := f(0);
  assert false;
  }
  lemma FalseProof() {
  ghost var identity: Empty -> Empty := x => x;
  assert exists f: Empty -> Empty {:nowarn} :: true;
  var s: S<Empty> := Box(true);
  var t: S<int> := s;
  Contradiction(t);
  assert false;
  }
}

module Nonvariant {
  type Empty = x: int | false witness *
  ghost predicate F<X(!new)>(y: X) { false }
  ghost predicate Some<X(!new)>() { exists y: X {:nowarn} :: true }
  datatype Box<+X(!new)> = Box(b: bool)
  datatype G<!X(!new)> = G(ghost f: X -> Empty, ok: bool)
  type S<+X(!new)> = s: Box<X> | s.b ==> (exists g: G<X> :: g.ok) witness *
  lemma Contradiction(t: S<int>) requires t.b ensures false {
  var g: G<int> :| g.ok;
  var impossible := g.f(0);
  assert false;
  }
  lemma FalseProof() {
  ghost var identity: Empty -> Empty := x => x;
  assert exists g: G<Empty> :: g.ok by { assert G<Empty>.G(identity, true).ok; }
  var s: S<Empty> := Box(true);
  var t: S<int> := s;
  Contradiction(t);
  assert false;
  }
}

module Exists {
  type Empty = x: nat | false witness *
  ghost predicate P<X(!new)>(x: X) { true }
  datatype Box<-X(!new)> = Box(b: bool)
  type S<-X(!new)> = s: Box<X> | s.b ==> (exists y: X :: P(y)) witness *
  lemma Contradiction(t: S<Empty>) requires t.b ensures false {
    var impossible: Empty :| true;
    assert false;
  }
  lemma FalseProof() {
    assert 0 is nat;
    assert P<nat>(0);
    assert exists y: nat :: P(y);
    var s: S<nat> := Box<nat>.Box(true);
    var t: S<Empty> := s;
    Contradiction(t);
    assert false;
  }
}


module DeclaredContravariance {
  type Empty = x: int | false witness *
  ghost predicate F<X(!new)>(y: X) { false }
  ghost predicate Some<X(!new)>() { exists y: X {:nowarn} :: true }
  datatype Box<+X(!new)> = Box(b: bool)
  datatype G<-X(!new)> = G(ghost f: X -> Empty, ok: bool)
  type S<+X(!new)> = s: Box<X> | s.b ==> (exists g: G<X> :: g.ok) witness *
  lemma Contradiction(t: S<int>) requires t.b ensures false {
  var g: G<int> :| g.ok;
  var impossible := g.f(0);
  assert false;
  }
  lemma FalseProof() {
  ghost var identity: Empty -> Empty := x => x;
  assert exists g: G<Empty> :: g.ok by { assert G<Empty>.G(identity, true).ok; }
  var s: S<Empty> := Box(true);
  var t: S<int> := s;
  Contradiction(t);
  assert false;
  }
}
