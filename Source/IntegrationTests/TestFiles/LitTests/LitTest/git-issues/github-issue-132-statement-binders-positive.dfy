// RUN: echo "refresh=false axioms=false" > "%t"
// RUN: %exits-with 0 %baredafny verify "%s" --type-system-refresh=false --additional-axioms=false --show-snippets=false --use-basename-for-filename --solver-path "%review-z3" --verification-time-limit=60 --cores=1 >> "%t"
// RUN: echo "refresh=false axioms=true" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%s" --type-system-refresh=false --additional-axioms=true --show-snippets=false --use-basename-for-filename --solver-path "%review-z3" --verification-time-limit=60 --cores=1 >> "%t"
// RUN: echo "refresh=true axioms=false" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%s" --type-system-refresh=true --additional-axioms=false --show-snippets=false --use-basename-for-filename --solver-path "%review-z3" --verification-time-limit=60 --cores=1 >> "%t"
// RUN: echo "refresh=true axioms=true" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%s" --type-system-refresh=true --additional-axioms=true --show-snippets=false --use-basename-for-filename --solver-path "%review-z3" --verification-time-limit=60 --cores=1 >> "%t"
// RUN: %diff "%s.expect" "%t"

class C {}
datatype Wrapped = Wrap(value: C)
function Identity(c: C): C { c }

method AssertionLocal(c: C) {
  var f := () => (assert true by { var x := c; assert x == c; } 0);
  assert allocated(f);
  assert f() == 0;
}

method CalculationLocal(c: C) {
  var f := () => (calc { 0; { var x := c; assert x == c; } 0; } 0);
  assert allocated(f);
  assert f() == 0;
}

method PatternLocal(c: C) {
  var f := () => (assert true by { var Wrap(x) := Wrap(c); assert x == c; } 0);
  assert allocated(f);
  assert f() == 0;
}

method MatchLocal(c: C) {
  var f := () => (assert true by { match Wrap(c) { case Wrap(x) => assert x == c; } } 0);
  assert allocated(f);
  assert f() == 0;
}

method BindingGuard(c: C) {
  var f := () => (assert true by { if x: C :| x in {c} && Identity(x) == c { assert x == c; } } 0);
  assert allocated(f);
  assert f() == 0;
}

method LocalLabel(c: C) {
  var f := () => (assert true by { label L: assert old@L(allocated(c)); } 0);
  assert allocated(f);
  assert f() == 0;
}

method OuterLabel(c: C) {
  label L:
  var f := () => (assert old@L(allocated(c)); 0);
  assert allocated(f);
  assert f() == 0;
}
