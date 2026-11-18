// RUN: %exits-with 2 %verify --type-system-refresh --general-traits=datatype --member-invariants "%s" > "%t"
// RUN: %verify --type-system-refresh --general-traits=datatype --member-invariants --legacy-member-invariant-dependence "%s" >> "%t"
// RUN: %exits-with 2 %resolve --type-system-refresh=false --member-invariants "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Every WouldBeBad proves false under the legacy cache; each unbounded quantifier must be rejected.

module EnterB {
  datatype Liar = Liar(n: int) {
    invariant No()
  }
  datatype B = Loop(c: C) | End(t: Liar)
  datatype C = C(b: B)
  ghost predicate No() {
    (forall b: B | b in [] :: true) && !(exists c: C {:nowarn} :: true)
  }
  ghost function Extract(c: C): Liar {
    match c.b
    case End(t) => t
    case Loop(next) => Extract(next)
  }
  lemma HasValue(c: C) ensures exists x: C {:nowarn} :: true { }
  lemma WouldBeBad() ensures false {
    if No() {
      var t: Liar := Liar(0);
      var c := C(B.End(t));
      HasValue(c);
    } else {
      var c: C :| true;
      var t := Extract(c);
      assert No();
    }
  }
}

module EnterC {
  datatype Liar = Liar(n: int) {
    invariant No()
  }
  datatype C = Loop(c: B) | End(t: Liar)
  datatype B = B(c: C)
  ghost predicate No() {
    (forall b: C | b in [] :: true) && !(exists c: B {:nowarn} :: true)
  }
  ghost function Extract(c: B): Liar {
    match c.c
    case End(t) => t
    case Loop(next) => Extract(next)
  }
  lemma HasValue(c: B) ensures exists x: B {:nowarn} :: true { }
  lemma WouldBeBad() ensures false {
    if No() {
      var t: Liar := Liar(0);
      var c := B(C.End(t));
      HasValue(c);
    } else {
      var c: B :| true;
      var t := Extract(c);
      assert No();
    }
  }
}

module ThreeEnterB {
  datatype Liar = Liar(n: int) {
    invariant No()
  }
  datatype B = Loop(c: C) | End(t: Liar)
  datatype C = C(d: D)
  datatype D = D(b: B)
  ghost predicate No() {
    (forall b: B | b in [] :: true) && !(exists c: C {:nowarn} :: true)
  }
  ghost function Extract(c: C): Liar {
    match c.d.b
    case End(t) => t
    case Loop(next) => Extract(next)
  }
  lemma HasValue(c: C) ensures exists x: C {:nowarn} :: true { }
  lemma WouldBeBad() ensures false {
    if No() {
      var t: Liar := Liar(0);
      var c := C(D(B.End(t)));
      HasValue(c);
    } else {
      var c: C :| true;
      var t := Extract(c);
      assert No();
    }
  }
}

module ThreeEnterC {
  datatype Liar = Liar(n: int) {
    invariant No()
  }
  datatype C = Loop(c: D) | End(t: Liar)
  datatype D = D(b: B)
  datatype B = B(c: C)
  ghost predicate No() {
    (forall b: C | b in [] :: true) && !(exists c: D {:nowarn} :: true)
  }
  ghost function Extract(c: D): Liar {
    match c.b.c
    case End(t) => t
    case Loop(next) => Extract(next)
  }
  lemma HasValue(c: D) ensures exists x: D {:nowarn} :: true { }
  lemma WouldBeBad() ensures false {
    if No() {
      var t: Liar := Liar(0);
      var c := D(B(C.End(t)));
      HasValue(c);
    } else {
      var c: D :| true;
      var t := Extract(c);
      assert No();
    }
  }
}

module ThreeEnterD {
  datatype Liar = Liar(n: int) {
    invariant No()
  }
  datatype D = Loop(c: B) | End(t: Liar)
  datatype B = B(c: C)
  datatype C = C(d: D)
  ghost predicate No() {
    (forall b: D | b in [] :: true) && !(exists c: B {:nowarn} :: true)
  }
  ghost function Extract(c: B): Liar {
    match c.c.d
    case End(t) => t
    case Loop(next) => Extract(next)
  }
  lemma HasValue(c: B) ensures exists x: B {:nowarn} :: true { }
  lemma WouldBeBad() ensures false {
    if No() {
      var t: Liar := Liar(0);
      var c := B(C(D.End(t)));
      HasValue(c);
    } else {
      var c: B :| true;
      var t := Extract(c);
      assert No();
    }
  }
}
