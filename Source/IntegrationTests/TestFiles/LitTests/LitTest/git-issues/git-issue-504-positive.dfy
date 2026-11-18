// RUN: %verify --type-system-refresh --general-traits=datatype --member-invariants "%s" > "%t"
// RUN: %exits-with 4 %verify --type-system-refresh --general-traits=datatype --member-invariants "%S/Inputs/git-issue-504-false.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Independent datatype cycles may be quantified over without a collection bound.

module TwoIndependent {
  datatype B = Loop(c: C) | End(n: int)
  datatype C = C(b: B)
  datatype Good = Good(n: int) {
    invariant Independent()
  }
  ghost predicate Independent() {
    (forall b: B | b in [] :: true) && (exists c: C {:nowarn} :: true)
  }
  lemma IndependentExists() ensures Independent() {
    var c := C(B.End(0));
    assert exists x: C {:nowarn} :: true;
  }
  lemma Construct() returns (g: Good) {
    IndependentExists();
    g := Good(1);
  }
}

module ThreeIndependent {
  datatype B = Loop(c: C) | End(n: int)
  datatype C = C(d: D)
  datatype D = D(b: B)
  datatype Good = Good(n: int) {
    invariant Independent()
  }
  ghost predicate Independent() {
    (forall b: B | b in [] :: true) && (exists c: C {:nowarn} :: true)
  }
  lemma IndependentExists() ensures Independent() {
    var c := C(D(B.End(0)));
    assert exists x: C {:nowarn} :: true;
  }
  lemma Construct() returns (g: Good) {
    IndependentExists();
    g := Good(1);
  }
}
