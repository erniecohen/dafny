// RUN: %exits-with 4 %verify --solver-path "%review-z3" "%s" > "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms "%s" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/496 already verifies: after asserting the
// constraint at the base newtype, its integer consequence follows. The #495 positive
// control checks the predicate and the lower bound, but not this upper bound.
newtype NewSix = x | 6 <= x witness 6
predicate Small(x: NewSix) { x < 100 }
newtype Explicit = x: NewSix | Small(x) witness 6
newtype Inferred = x | Small(x) witness 6

method ExplicitBase(y: Explicit) {
  assert Small(y as NewSix);
  assert y as int < 100;
}

method InferredBase(y: Inferred) {
  assert Small(y as NewSix);
  assert y as int < 100;
}

// Both types are inhabited, including at the upper endpoint minus one.
method Witnesses() {
  var a := 6 as Explicit;
  var b := 99 as Explicit;
  var c := 6 as Inferred;
  var d := 99 as Inferred;
  assert a as int == 6 && b as int == 99;
  assert c as int == 6 && d as int == 99;
}

// The same constraint and cast context must not make arbitrary assertions provable.
method ExplicitNonvacuity(y: Explicit) {
  assert Small(y as NewSix);
  assert y as int < 100;
  assert false; // error
}

method InferredNonvacuity(y: Inferred) {
  assert Small(y as NewSix);
  assert y as int < 100;
  assert false; // error
}
