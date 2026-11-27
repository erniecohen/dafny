// RUN: %verify --solver-path "%review-z3" --function-extensionality-axiom --additional-axioms "%s" > "%t"
// RUN: %verify --solver-path "%review-z3" --function-extensionality-axiom --additional-axioms --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" "%s" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms "%s" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --function-extensionality-axiom --additional-axioms "%S/Inputs/git-issue-374-partial-equality-negative.dfy" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --function-extensionality-axiom --additional-axioms --type-system-refresh=false --general-newtypes=false "%S/Inputs/git-issue-374-partial-equality-negative.dfy" >> "%t"
// RUN: %verify --solver-path "%review-z3" --function-extensionality-axiom --filter-symbol Automatic "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

lemma Automatic0(f: () --> int, g: () --> int)
  requires f.requires() == g.requires()
  requires f.requires() ==> f() == g()
  ensures f == g
  ensures !(f != g)
{}
lemma Automatic1(f: int --> int, g: int --> int)
  requires forall x0: int :: f.requires(x0) == g.requires(x0)
  requires forall x0: int :: f.requires(x0) ==> f(x0) == g(x0)
  ensures f == g
  ensures !(f != g)
{}
lemma Automatic2(f: (int, bool) --> int, g: (int, bool) --> int)
  requires forall x0: int, x1: bool :: f.requires(x0, x1) == g.requires(x0, x1)
  requires forall x0: int, x1: bool :: f.requires(x0, x1) ==> f(x0, x1) == g(x0, x1)
  ensures f == g
  ensures !(f != g)
{}
lemma Automatic3(f: (int, bool, real) --> int, g: (int, bool, real) --> int)
  requires forall x0: int, x1: bool, x2: real :: f.requires(x0, x1, x2) == g.requires(x0, x1, x2)
  requires forall x0: int, x1: bool, x2: real :: f.requires(x0, x1, x2) ==> f(x0, x1, x2) == g(x0, x1, x2)
  ensures f == g
  ensures !(f != g)
{}
lemma Automatic4(f: (int, bool, real, int) --> int, g: (int, bool, real, int) --> int)
  requires forall x0: int, x1: bool, x2: real, x3: int :: f.requires(x0, x1, x2, x3) == g.requires(x0, x1, x2, x3)
  requires forall x0: int, x1: bool, x2: real, x3: int :: f.requires(x0, x1, x2, x3) ==> f(x0, x1, x2, x3) == g(x0, x1, x2, x3)
  ensures f == g
  ensures !(f != g)
{}
lemma Automatic5(f: (int, bool, real, int, int) --> int, g: (int, bool, real, int, int) --> int)
  requires forall x0: int, x1: bool, x2: real, x3: int, x4: int :: f.requires(x0, x1, x2, x3, x4) == g.requires(x0, x1, x2, x3, x4)
  requires forall x0: int, x1: bool, x2: real, x3: int, x4: int :: f.requires(x0, x1, x2, x3, x4) ==> f(x0, x1, x2, x3, x4) == g(x0, x1, x2, x3, x4)
  ensures f == g
  ensures !(f != g)
{}
lemma OutsidePrecondition() {
  var f: int --> int := (x: int) requires x >= 0 => x;
  var g: int --> int := (x: int) requires x >= 0 => if x < 0 then 7 else x;
  assert forall x: int :: f.requires(x) == g.requires(x);
  assert forall x: int :: f.requires(x) ==> f(x) == g(x);
  assert f == g;
}
function Named(x: int): int requires x >= 0 { x }
lemma NamedAndLambda() {
  var g: int --> int := (x: int) requires x >= 0 => x + 0;
  assert forall x: int :: Named.requires(x) == g.requires(x);
  assert forall x: int :: Named.requires(x) ==> Named(x) == g(x);
  assert Named == g;
}
lemma EmptyPrecondition() {
  var f: () --> int := () requires false => 0;
  var g: () --> int := () requires false => 1;
  assert !f.requires() && !g.requires();
  assert f == g;
}
lemma Mixed(f: int -> int, g: int --> int)
  requires forall x :: g.requires(x)
  requires forall x :: f(x) == g(x)
  ensures f == g && g == f
{}
