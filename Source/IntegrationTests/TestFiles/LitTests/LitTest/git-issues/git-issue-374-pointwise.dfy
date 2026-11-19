// NONUNIFORM: verifies an opt-in source library outside the test directory.
// RUN: %verify --solver-path "%review-z3" --additional-axioms --verify-included-files "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Pointwise.dfy" "%s" > "%t"
// RUN: %verify --solver-path "%review-z3" --function-extensionality-axiom --verify-included-files "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Pointwise.dfy" "%s" >> "%t"
// RUN: %verify --solver-path "%review-z3" --additional-axioms --type-system-refresh=false --general-newtypes=false --verify-included-files "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Pointwise.dfy" "%s" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms --verify-included-files "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Pointwise.dfy" "%S/Inputs/git-issue-374-pointwise-negative.dfy" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --function-extensionality-axiom --verify-included-files "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Pointwise.dfy" "%S/Inputs/git-issue-374-pointwise-negative.dfy" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms --type-system-refresh=false --general-newtypes=false --verify-included-files "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Pointwise.dfy" "%S/Inputs/git-issue-374-pointwise-negative.dfy" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Pointwise.dfy" "%S/Inputs/git-issue-374-pointwise-effects.dfy" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms --type-system-refresh=false --general-newtypes=false "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Pointwise.dfy" "%S/Inputs/git-issue-374-pointwise-effects.dfy" >> "%t"
// RUN: %audit --report-file "%t.audit.md" "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Pointwise.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

import D = Std.FunctionDomains
import P = Std.FunctionPointwise
class C { var n: int }
datatype WithReferences = Wrap(c: C, bit: bool)

lemma Generic<A, B>(f: A -> B, g: A -> B)
  requires D.Arity1.ExactDom(f) && D.Arity1.ExactDom(g)
  requires P.Arity1.Pointwise(f, g)
  ensures f == g
{
  P.Arity1.Extensionality(f, g);
}

lemma Identity() {
  var f := (c: C) => c;
  var g := (c: C) => if true then c else c;
  assert P.Arity1.Pointwise<C, C>(f, g);
  P.Arity1.Extensionality<C, C>(f, g);
  assert f == g;
}
function Named(c: C): C { c }
lemma NamedAndLambda() {
  var f := (c: C) => c;
  assert forall c: C :: f(c) == Named(c);
  assert P.Arity1.Pointwise<C, C>(f, Named);
  P.Arity1.Extensionality<C, C>(f, Named);
}
lemma DatatypeDomain() {
  var f := (d: WithReferences) => d.c;
  var g := (d: WithReferences) => if d.bit then d.c else d.c;
  assert P.Arity1.Pointwise<WithReferences, C>(f, g);
  P.Arity1.Extensionality<WithReferences, C>(f, g);
}
lemma RestrictedDomain() {
  var f: C -> int := (o: object) => 0;
  var g := (c: C) => 1 - 1;
  var r := D.Arity1.Restrict<C, int>(f);
  assert P.Arity1.Pointwise<C, int>(r, g);
  P.Arity1.Extensionality<C, int>(r, g);
}
lemma Binary() {
  var f := (c: C, cs: seq<C>) => c;
  var g := (c: C, cs: seq<C>) => if |cs| == 0 then c else c;
  assert P.Arity2.Pointwise<C, seq<C>, C>(f, g);
  P.Arity2.Extensionality<C, seq<C>, C>(f, g);
}
lemma Ternary() {
  var f := (c: C, n: int, b: bool) => if b then c else c;
  var g := (c: C, n: int, b: bool) => c;
  assert P.Arity3.Pointwise<C, int, bool, C>(f, g);
  P.Arity3.Extensionality<C, int, bool, C>(f, g);
}
lemma Quaternary() {
  var f := (a: C, b: C, c: C, d: C) => (a, b, c, d);
  var g := (a: C, b: C, c: C, d: C) => if a == b then (a, a, c, d) else (a, b, c, d);
  assert P.Arity4.Pointwise<C, C, C, C, (C, C, C, C)>(f, g);
  P.Arity4.Extensionality<C, C, C, C, (C, C, C, C)>(f, g);
}
type EmptyRef = c: C | false witness *
lemma EmptyProduct() {
  var f := (c: C, e: EmptyRef) => 0;
  var g := (c: C, e: EmptyRef) => 1;
  assert P.Arity2.Pointwise<C, EmptyRef, int>(f, g);
  P.Arity2.Extensionality<C, EmptyRef, int>(f, g);
}
method FutureAllocation(f: C -> int, g: C -> int)
  requires D.Arity1.ExactDom<C, int>(f) && D.Arity1.ExactDom<C, int>(g)
  requires P.Arity1.Pointwise<C, int>(f, g)
{
  var c := new C;
  P.Arity1.Extensionality<C, int>(f, g);
  assert f(c) == g(c);
}
method CapturedSnapshot(c: C)
  modifies c
{
  var before := c.n;
  var f := (x: C) => before;
  c.n := c.n + 1;
  var g := (x: C) => before;
  assert P.Arity1.Pointwise<C, int>(f, g);
  P.Arity1.Extensionality<C, int>(f, g);
}
lemma NoReferences<A(!new), B>(f: A -> B, g: A -> B)
  requires forall x :: f(x) == g(x)
  ensures P.Arity1.Pointwise(f, g)
{}

lemma FirstClassPredicate() {
  var p := P.Arity1.Pointwise<C, int>;
  var f := (c: C) => 0;
  var g := (c: C) => 1 - 1;
  assert p(f, g);
  P.Arity1.Extensionality<C, int>(f, g);
}
