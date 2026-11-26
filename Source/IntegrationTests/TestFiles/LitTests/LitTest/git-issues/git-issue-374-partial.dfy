// NONUNIFORM: checks an opt-in source library and full-domain partial agreement.
// RUN: %verify --solver-path "%review-z3" --additional-axioms --heap-free-lambda-facts --verify-included-files "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Partial.dfy" "%s" > "%t"
// RUN: %verify --solver-path "%review-z3" --additional-axioms --heap-free-lambda-facts --type-system-refresh=false --general-newtypes=false --verify-included-files "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Partial.dfy" "%s" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms --heap-free-lambda-facts "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Partial.dfy" "%S/Inputs/git-issue-374-partial-negative.dfy" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms --heap-free-lambda-facts --type-system-refresh=false --general-newtypes=false "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Partial.dfy" "%S/Inputs/git-issue-374-partial-negative.dfy" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms --heap-free-lambda-facts "%S/Inputs/git-issue-374-partial-malformed.dfy" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms --heap-free-lambda-facts --type-system-refresh=false --general-newtypes=false "%S/Inputs/git-issue-374-partial-malformed.dfy" >> "%t"
// RUN: %audit --report-file "%t.audit.md" "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Partial.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

import P = Std.PartialFunctionDomains

lemma Generic<A, B>(f: A --> B, g: A --> B)
  requires P.Arity1.ExactDom(f) && P.Arity1.ExactDom(g)
  requires P.Arity1.Pointwise(f, g)
  ensures f == g
{
  P.Arity1.Extensionality(f, g);
}
lemma Numeric(f: int --> int, g: int --> int)
  requires forall x :: f.requires(x) == g.requires(x)
  requires forall x :: f.requires(x) ==> f(x) == g(x)
  ensures f == g
{
  assert P.Arity1.Pointwise(f, g);
  P.Arity1.Extensionality(f, g);
}
lemma OutsidePrecondition() {
  var f: int --> int := (x: int) requires x >= 0 => x;
  var g: int --> int := (x: int) requires x >= 0 => if x < 0 then 7 else x;
  assert P.Arity1.Pointwise(f, g);
  P.Arity1.Extensionality(f, g);
  assert f == g;
  assert f(0) == 0;
}
lemma SubsetDomain() {
  var f: nat --> int := (x: nat) requires x >= 2 => x;
  var g: nat --> int := (x: nat) requires x >= 2 => if x == 1 then 7 else x;
  assert P.Arity1.ExactDom<nat, int>(f) && P.Arity1.ExactDom<nat, int>(g);
  P.Arity1.Extensionality<nat, int>(f, g);
  assert f == g;
  assert f(2) == 2;
}
function Named(x: int): int requires x >= 0 { x }
lemma NamedAndLambda() {
  var g: int --> int := (x: int) requires x >= 0 => if x < 0 then 7 else x;
  assert P.Arity1.Pointwise(Named, g);
  P.Arity1.Extensionality(Named, g);
  assert Named == g;
}
lemma EmptyPrecondition() {
  var f: () --> int := () requires false => 0;
  var g: () --> int := () requires false => 1;
  P.Arity0.Extensionality(f, g);
  assert f == g;
}
lemma Nullary() {
  var f: () --> int := () requires true => 3;
  var g: () --> int := () requires true => 1 + 2;
  P.Arity0.Extensionality(f, g);
}
lemma Binary() {
  var f: (int, bool) --> int := (x: int, b: bool) requires x >= 0 => x;
  var g: (int, bool) --> int := (x: int, b: bool) requires x >= 0 => if x < 0 then 7 else x;
  P.Arity2.Extensionality(f, g);
  assert f == g;
}
lemma Ternary() {
  var f: (int, bool, real) --> real := (x: int, b: bool, y: real) requires x >= 0 => y;
  var g: (int, bool, real) --> real := (x: int, b: bool, y: real) requires x >= 0 => if x < 0 then 7.0 else y;
  P.Arity3.Extensionality(f, g);
  assert f == g;
}
lemma Quaternary() {
  var f: (int, bool, real, int) --> int := (a: int, b: bool, c: real, d: int) requires a >= 0 => d;
  var g: (int, bool, real, int) --> int := (a: int, b: bool, c: real, d: int) requires a >= 0 => if a < 0 then 7 else d;
  P.Arity4.Extensionality(f, g);
  assert f == g;
}
class Cell {}
lemma References() {
  var f: Cell --> Cell := (c: Cell) requires true => c;
  var g: Cell --> Cell := (c: Cell) requires true => if true then c else c;
  P.Arity1.Extensionality<Cell, Cell>(f, g);
}
lemma FirstClass() {
  var p := P.Arity1.Pointwise<int, int>;
  var f: int --> int := (x: int) requires x >= 0 => x;
  var g: int --> int := (x: int) requires x >= 0 => x + 0;
  assert p(f, g);
  P.Arity1.Extensionality(f, g);
}

lemma Projection(f: int --> int, g: int --> int, x: int)
  requires P.Arity1.Pointwise(f, g)
  ensures f.requires(x) == g.requires(x)
  ensures f.requires(x) ==> f(x) == g(x)
{}
module Five {
  ghost predicate {:functionDomain "partialExactDom"} ExactDom<A, B, C, D, E, R>(f: (A, B, C, D, E) --> R)
  ghost predicate {:functionDomain "partialPointwise"} Pointwise<A, B, C, D, E, R>(f: (A, B, C, D, E) --> R, g: (A, B, C, D, E) --> R)
  ghost predicate {:no_inline} {:functionDomain "partialEqual"} Equal<A, B, C, D, E, R>(f: (A, B, C, D, E) --> R, g: (A, B, C, D, E) --> R) { f == g }
  lemma Generic<A, B, C, D, E, R>(f: (A, B, C, D, E) --> R, g: (A, B, C, D, E) --> R)
    requires ExactDom(f) && ExactDom(g) && Pointwise(f, g)
    ensures f == g
  {
    assert Equal(f, g);
  }
}
