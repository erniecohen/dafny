// RUN: %exits-with 4 %verify --solver-path "%review-z3" "%s" > "%t"
// RUN: %verify --solver-path "%review-z3" --additional-axioms "%s" >> "%t"
// RUN: %verify --solver-path "%review-z3" --function-extensionality-axiom "%s" >> "%t"
// RUN: %verify --solver-path "%review-z3" --additional-axioms --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms "%S/Inputs/git-issue-374-pointwise-malformed.dfy" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms --type-system-refresh=false --general-newtypes=false "%S/Inputs/git-issue-374-pointwise-malformed.dfy" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms "%S/Inputs/git-issue-374-pointwise-intrinsic-negative.dfy" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --function-extensionality-axiom "%S/Inputs/git-issue-374-pointwise-intrinsic-negative.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

module Zero {
  ghost predicate {:functionDomain "pointwise"} Pointwise<R>(f: () -> R, g: () -> R)
}
module Five {
  ghost predicate {:functionDomain "pointwise"} Pointwise<A, B, C, D, E, R>(f: (A, B, C, D, E) -> R, g: (A, B, C, D, E) -> R)
}
lemma Nullary<R>(f: () -> R, g: () -> R)
  requires f() == g()
  ensures Zero.Pointwise(f, g)
{}
lemma Introduction<A(!new), B(!new), C(!new), D(!new), E(!new), R>(f: (A, B, C, D, E) -> R, g: (A, B, C, D, E) -> R)
  requires forall a, b, c, d, e :: f(a, b, c, d, e) == g(a, b, c, d, e)
  ensures Five.Pointwise(f, g)
{}
lemma Projection<A, B, C, D, E, R>(f: (A, B, C, D, E) -> R, g: (A, B, C, D, E) -> R, a: A, b: B, c: C, d: D, e: E)
  requires Five.Pointwise(f, g)
  ensures f(a, b, c, d, e) == g(a, b, c, d, e)
{}
