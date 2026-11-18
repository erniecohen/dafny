// Part of https://github.com/erniecohen/dafny/issues/89 (see ../github-issue-89.dfy): under
// --general-traits=full, datatypes and newtypes inherit trait axioms too.  Without
// --inherited-trait-axioms, each type below is rejected.

trait T {
  ghost function F(): int
  lemma {:axiom} Positive() ensures F() > 0
  ghost function {:axiom} G(): nat ensures G() < 10
}

datatype D extends T = D(x: int) {
  ghost function F(): int { if x > 0 then x else 1 }
}

newtype N extends T = x: int | 0 <= x < 100 {
  ghost function F(): int { (this as int) + 1 }
}

lemma UseDatatype(d: D) ensures d.F() > 0 && d.G() < 10 { d.Positive(); }
lemma UseNewtype(n: N) ensures n.F() > 0 && n.G() < 10 { n.Positive(); }
lemma ControlDatatype(d: D) { d.Positive(); assert d.F() > 0 && d.G() < 10; assert false; }  // error
lemma ControlNewtype(n: N) { n.Positive(); assert n.F() > 0 && n.G() < 10; assert false; }  // error
