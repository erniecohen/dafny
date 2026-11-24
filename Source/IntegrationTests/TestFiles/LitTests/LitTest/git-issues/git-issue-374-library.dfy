// NONUNIFORM: checks a source library outside the test directory, including its proofs.
// RUN: %verify --solver-path "%review-z3" --additional-axioms --show-snippets=false "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Functions.dfy" "%s" > "%t"
// RUN: %verify --solver-path "%review-z3" --function-extensionality-axiom --show-snippets=false "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Functions.dfy" "%s" >> "%t"
// RUN: %verify --solver-path "%review-z3" --additional-axioms --type-system-refresh=false --general-newtypes=false --show-snippets=false "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Functions.dfy" "%s" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms --show-snippets=false "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Functions.dfy" "%S/Inputs/git-issue-374-library-negative.dfy" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --function-extensionality-axiom --show-snippets=false "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Functions.dfy" "%S/Inputs/git-issue-374-library-negative.dfy" >> "%t"
// RUN: %audit --report-file "%t.audit.md" "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Functions.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

import FD = Std.FunctionDomains

lemma Nullary(f: () -> int, g: () -> int)
  requires f() == g()
  ensures f == g
{
  FD.Arity0.Restriction(f);
  FD.Arity0.Extensionality(f, g);
}
lemma Unary<A(!new), R>(f: A -> R, g: A -> R)
  requires forall a :: f(a) == g(a)
  ensures FD.Arity1.Restrict(f) == FD.Arity1.Restrict(g)
{
  FD.Arity1.Restriction(f);
  FD.Arity1.Restriction(g);
  FD.Arity1.Extensionality(FD.Arity1.Restrict(f), FD.Arity1.Restrict(g));
}
lemma Binary() {
  var f: (nat, nat) -> int := (x: int, y: int) => if x < 0 then 7 else x + y;
  var g: (nat, nat) -> int := (x: int, y: int) => x + y;
  FD.Arity2.Restriction<nat, nat, int>(f);
  FD.Arity2.Restriction<nat, nat, int>(g);
  FD.Arity2.Extensionality<nat, nat, int>(FD.Arity2.Restrict<nat, nat, int>(f), FD.Arity2.Restrict<nat, nat, int>(g));
}
datatype D = One | Two
function Named(d: D, n: nat, b: bool): D { if b then d else One }
lemma Ternary() {
  var f: (D, nat, bool) -> D := (d: D, n: nat, b: bool) => if b then d else One;
  assert FD.Arity3.ExactDom<D, nat, bool, D>(f) && FD.Arity3.ExactDom<D, nat, bool, D>(Named);
  FD.Arity3.Extensionality<D, nat, bool, D>(f, Named);
}
lemma Quaternary(f: (int, bool, real, int) -> D, g: (int, bool, real, int) -> D)
  requires forall a, b, c, d :: f(a, b, c, d) == g(a, b, c, d)
  ensures f == g
{
  FD.Arity4.Extensionality(f, g);
}
