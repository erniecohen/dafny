// RUN: %exits-with 4 %verify --type-system-refresh --general-newtypes --general-traits=full --member-invariants --consistent-obligation-checks --solver-path "%review-z3" --cores 1 "%s" > "%t"
// RUN: %exits-with 4 %verify --type-system-refresh --general-newtypes --general-traits=full --member-invariants --consistent-obligation-checks --additional-axioms --solver-path "%review-z3" --cores 1 "%s" >> "%t"
// RUN: %exits-with 4 %verify --type-system-refresh --general-newtypes --general-traits=full --member-invariants --solver-path "%review-z3" --cores 1 "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Both the inherited invariant and the newtype's invariant need a native receiver.
trait G {
  const k: int := 1
  invariant k == 1
  function F(): int { k }
}
newtype N extends G = x: int | x < 10 {
  invariant this as int >= 0
  witness 0
}
lemma RoundTrip() {
  var n := 1 as N;
  var g := n as G;
  var back := g as N;
  assert back == n;
  assert g.k == 1;
  assert g.F() == 1;
}
lemma UncheckedDowncast(g: G) {
  var n := g as N;
}
lemma WrongConstraint() {
  var n := 10 as N;
}
lemma WrongInvariant() {
  var n := -1 as N;
}
lemma FalseAfterRoundTrip() {
  var n := 1 as N;
  var g := n as G;
  var back := g as N;
  assert false;
}
