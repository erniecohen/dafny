// RUN: %exits-with 4 %verify --type-system-refresh --general-newtypes --general-traits=full --consistent-obligation-checks --solver-path "%review-z3" --cores 1 "%s" > "%t"
// RUN: %exits-with 4 %verify --type-system-refresh --general-newtypes --general-traits=full --consistent-obligation-checks --additional-axioms --solver-path "%review-z3" --cores 1 "%s" >> "%t"
// RUN: %exits-with 4 %verify --type-system-refresh --general-newtypes --general-traits=full --solver-path "%review-z3" --cores 1 "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %verify --type-system-refresh --general-newtypes --general-traits=full --consistent-obligation-checks --legacy-boxed-newtype-obligations --solver-path "%review-z3" --cores 1 "%s" > "%t.legacy"
// RUN: %diff "%s.legacy.expect" "%t.legacy"

// A true constraint does not establish that the receiver contains an integer.
trait G {
  const k: int := 1
  function F(): int
}
newtype I extends G = x: int | true {
  function F(): int { 1 }
}
datatype D extends G = D(b: bool) {
  function F(): int { 0 }
}
lemma Witness() {
  var n := 0 as I;
  var g := n as G;
  var back := g as I;
  assert back == n;
  assert g.F() == 1;
}
lemma UncheckedDowncast(g: G) {
  var n := g as I; // error
}
lemma WrongRepresentation() {
  var g: G := D(true);
  var n := g as I; // error
}
